namespace Demo
{
    using System.Collections.Generic;

    public class Server
    {
        // Connected clients and their entities.
        public List<Client> clients = new List<Client>();

        public List<Entity> entities = new List<Entity>();

        // Last processed input for each client.
        public List<int> last_processed_input = new List<int>() { 0, 0 };

        // Simulated network connection.
        public LagNetwork network = new LagNetwork();

        public ulong update_rate;

        // :: Additions
        private ulong _nextUpdate;

        public Server()
        {
            // Default update rate.
            SetUpdateRate(10);
        }

        public void Connect(Client client)
        {
            // Give the Client enough data to identify itself.
            client.server = this;
            client.entity_id = this.clients.Count;
            this.clients.Add(client);

            // Create a new Entity for this Client.
            var entity = new Entity();
            this.entities.Add(entity);
            entity.entity_id = client.entity_id.Value;

            // Set the initial state of the Entity (e.g. spawn point)
            var spawn_points = new float[] { -3, 3 };
            entity.x = spawn_points[client.entity_id.Value];
        }

        public void SetUpdateRate(ulong hz)
        {
            this.update_rate = hz;
        }

        // :: Addition
        public void Tick()
        {
            var now = new Date();
            if (now < _nextUpdate)
            {
                return;
            }

            _nextUpdate = now + this.update_rate;
            Update();
        }

        private void Update()
        {
            ProcessInputs();
            SendWorldState();
            RenderWorld();
        }

        // Check whether this input seems to be valid (e.g. "make sense" according
        // to the physical rules of the World)
        private bool ValidateInput(Input input)
        {
            if (System.Math.Abs(input.press_time) > 1f / 40f)
            {
                return false;
            }

            return true;
        }

        private void ProcessInputs()
        {
            // Process all pending messages from clients.
            while (true)
            {
                var msg = this.network.Receive();
                if (msg == null)
                {
                    break;
                }

                // Update the state of the entity, based on its input.
                // We just ignore inputs that don't look valid; this is what prevents clients from cheating.
                var message = (Input)msg;
                //if (ValidateInput(message))
                {
                    var id = message.entity_id;
                    this.entities[id].ApplyInput(message);

                    if (this.entities[id].x < -5)
                    {
                        this.entities[id].x = -5;
                    }

                    if (this.last_processed_input.Count <= id)
                    {
                        this.last_processed_input.Add(message.input_sequence_number);
                    }
                    else
                    {
                        this.last_processed_input[id] = message.input_sequence_number;
                    }
                }
            }

            // Show some info.
            //var info = "Last acknowledged input: ";
            //for (var i = 0; i < this.clients.length; ++i)
            //{
            //    info += "Player " + i + ": #" + (this.last_processed_input[i] || 0) + "   ";
            //}
            //this.status.textContent = info;
        }

        // Send the world state to all the connected clients.
        private void SendWorldState()
        {
            // Gather the state of the world. In a real app, state could be filtered to avoid leaking data
            // (e.g. position of invisible enemies).
            var world_state = new List<EntityState>();
            var num_clients = this.clients.Count;
            for (var i = 0; i < num_clients; i++)
            {
                var entity = this.entities[i];
                world_state.Add(new EntityState()
                {
                    entity_id = entity.entity_id,
                    position = entity.x,
                    last_processed_input = this.last_processed_input[i]
                });
            }

            // Broadcast the state to all the clients.
            for (var i = 0; i < num_clients; i++)
            {
                var client = this.clients[i];
                client.network.Send(client.lag, world_state);
            }
        }

        private void RenderWorld()
        {
            // TODO ?
            LocalWorldCanvas.instance.RenderWorld(0, this.entities);
        }
    }
}