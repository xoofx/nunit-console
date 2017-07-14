using System;
using System.Collections;
using System.IO;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;

namespace NUnit.Engine.Internal
{
    partial class TcpChannelUtils
    {
        private sealed class ObservableServerChannelSinkProvider : IServerChannelSinkProvider
        {
            private readonly CurrentMessageCounter _currentMessageCounter;

            public ObservableServerChannelSinkProvider(CurrentMessageCounter currentMessageCounter)
            {
                if (currentMessageCounter == null) throw new ArgumentNullException(nameof(currentMessageCounter));
                _currentMessageCounter = currentMessageCounter;
            }

            public void GetChannelData(IChannelDataStore channelData)
            {
            }

            public IServerChannelSink CreateSink(IChannelReceiver channel)
            {
                if (Next == null)
                    throw new InvalidOperationException("Cannot create a sink without setting the next provider.");
                return new ObservableServerChannelSink(_currentMessageCounter, Next.CreateSink(channel));
            }

            public IServerChannelSinkProvider Next { get; set; }


            private sealed class ObservableServerChannelSink : IServerChannelSink
            {
                private readonly IServerChannelSink _next;
                private readonly CurrentMessageCounter _currentMessageCounter;

                public ObservableServerChannelSink(CurrentMessageCounter currentMessageCounter, IServerChannelSink next)
                {
                    if (next == null) throw new ArgumentNullException(nameof(next));
                    _currentMessageCounter = currentMessageCounter;
                    _next = next;
                }

                public IDictionary Properties => _next.Properties;

                public ServerProcessing ProcessMessage(IServerChannelSinkStack sinkStack, IMessage requestMsg,
                    ITransportHeaders requestHeaders, Stream requestStream, out IMessage responseMsg,
                    out ITransportHeaders responseHeaders, out Stream responseStream)
                {
                    _currentMessageCounter.OnMessageStart();

                    Stream innerResponseStream;
                    var processing = _next.ProcessMessage(sinkStack, requestMsg, requestHeaders, requestStream,
                        out responseMsg, out responseHeaders, out innerResponseStream);


                    /*
                    We don't have to wait for the socket to be closed because it is reused.
                     - http://referencesource.microsoft.com/#System.Runtime.Remoting/channels/tcp/tcpserverchannel.cs,639
                     - http://referencesource.microsoft.com/#System.Runtime.Remoting/channels/tcp/tcpstreams.cs,0372c865c0e99273

                    But we do have to wait for the entire response to be written to the SocketStream.

                    Whatever we return for responseStream will never be closed until it's written to the SocketStream.
                     - http://referencesource.microsoft.com/#System.Runtime.Remoting/channels/tcp/tcpserverchannel.cs,584
                     - http://referencesource.microsoft.com/#System.Runtime.Remoting/channels/tcp/tcpstreams.cs,365
                     - http://referencesource.microsoft.com/#System.Runtime.Remoting/channels/tcp/tcpserverchannel.cs,420

                    That will be safe unless the .NET Framework changes and starts buffering between innerResponseStream
                    and the SocketStream, because then closing responseStream won't tell us if the whole response
                    has been written out to the socket. But for now, and until we replace remoting, I'm going to put
                    my money here. No reflection needed.
                    */
                    responseStream = new NotifyOnCloseStream(innerResponseStream, _currentMessageCounter.OnMessageEnd);

                    return processing;
                }

                public void AsyncProcessResponse(IServerResponseChannelSinkStack sinkStack, object state, IMessage msg,
                    ITransportHeaders headers, Stream stream)
                {
                    _next.AsyncProcessResponse(sinkStack, state, msg, headers, stream);
                }

                public Stream GetResponseStream(IServerResponseChannelSinkStack sinkStack, object state, IMessage msg,
                    ITransportHeaders headers)
                {
                    return _next.GetResponseStream(sinkStack, state, msg, headers);
                }

                public IServerChannelSink NextChannelSink => _next.NextChannelSink;


                private delegate void Action();

                private sealed class NotifyOnCloseStream : Stream
                {
                    private readonly Stream _baseStream;
                    private readonly Action _onClose;

                    public NotifyOnCloseStream(Stream baseStream, Action onClose)
                    {
                        if (baseStream == null) throw new ArgumentNullException(nameof(baseStream));
                        if (onClose == null) throw new ArgumentNullException(nameof(onClose));
                        _baseStream = baseStream;
                        _onClose = onClose;
                    }

                    public override void Close()
                    {
                        base.Close();
                        _onClose.Invoke();
                    }

                    public override void Flush() => _baseStream.Flush();

                    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

                    public override void SetLength(long value) => _baseStream.SetLength(value);

                    public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);

                    public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);

                    public override bool CanRead => _baseStream.CanRead;

                    public override bool CanSeek => _baseStream.CanSeek;

                    public override bool CanWrite => _baseStream.CanWrite;

                    public override long Length => _baseStream.Length;

                    public override long Position
                    {
                        get { return _baseStream.Position; }
                        set { _baseStream.Position = value; }
                    }
                }
            }
        }
    }
}