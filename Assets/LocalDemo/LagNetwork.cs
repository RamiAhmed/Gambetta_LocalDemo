namespace Demo
{
    using System.Collections.Generic;

    public class LagNetwork
    {
        public List<Message> messages = new List<Message>();

        public void Send(int lag_ms, object message)
        {
            this.messages.Add(new Message()
            {
                recv_ts = new Date() + (ulong)lag_ms,
                payload = message
            });
        }

        public object Receive()
        {
            var now = new Date();
            for (var i = 0; i < this.messages.Count; i++)
            {
                var message = this.messages[i];
                if (message.recv_ts <= now)
                {
                    this.messages.RemoveAt(i);
                    return message.payload;
                }
            }

            return null;
        }
    }
}