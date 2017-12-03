namespace Demo
{
    using System.Collections.Generic;

    public class Client
    {
        // Local representation of the entities.
        public List<Entity> entities = new List<Entity>();

        // Input state.
        public bool key_left = false;

        public bool key_right = false;

        // Simulated network connection.
        public LagNetwork network = new LagNetwork();

        public Server server = null;
        public int lag = 0;

        // Unique ID of our entity. Assigned by Server on connection.
        public int? entity_id = null;

        // Data needed for reconciliation.
        public bool client_side_prediction = false;

        public bool server_reconciliation = false;
        public int input_sequence_number = 0;
        public List<Input> pending_inputs = new List<Input>();

        // Entity interpolation toggle.
        public bool entity_interpolation = true;

        // Update rate
        public ulong update_rate;

        public Date last_ts;

        // :: Additions
        private ulong _nextUpdate;

        public Client()
        {
            // Default update rate.
            SetUpdateRate(50);
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

        // Update Client state.
        private void Update()
        {
            // Listen to the server.
            this.ProcessServerMessages();

            if (this.entity_id == null)
            {
                return; // Not connected yet.
            }

            // Process inputs.
            this.ProcessInputs();

            // Interpolate other entities.
            if (this.entity_interpolation)
            {
                this.InterpolateEntities();
            }

            // Render the World.
            RenderWorld();

            // Show some info.
            //var info = "Non-acknowledged inputs: " + this.pending_inputs.length;
            //this.status.textContent = info;
        }

        // Get inputs and send them to the server.
        // If enabled, do client-side prediction.
        private void ProcessInputs()
        {
            // Compute delta time since last update.
            var now_ts = new Date();
            var last_ts = this.last_ts ?? now_ts;
            var dt_sec = (now_ts - last_ts) / 1000d;
            this.last_ts = now_ts;

            // Package player's input.
            Input input = null;
            if (this.key_right)
            {
                input = new Input()
                {
                    press_time = dt_sec
                };
            }
            else if (this.key_left)
            {
                input = new Input()
                {
                    press_time = -dt_sec
                };
            }
            else
            {
                // Nothing interesting happened.
                return;
            }

            // Send the input to the server.
            input.input_sequence_number = this.input_sequence_number++;
            input.entity_id = this.entity_id.Value;
            this.server.network.Send(this.lag, input);

            // Do client-side prediction.
            if (this.client_side_prediction)
            {
                this.entities[this.entity_id.Value].ApplyInput(input);
            }

            // Save this input for later reconciliation.
            this.pending_inputs.Add(input);
        }

        private void ProcessServerMessages()
        {
            while (true)
            {
                var msg = this.network.Receive();
                if (msg == null)
                {
                    break;
                }

                // :: Addition (cast to list of entity states)
                var message = (List<EntityState>)msg;

                // World state is a list of entity states.
                for (var i = 0; i < message.Count; i++)
                {
                    var state = message[i];

                    // If this is the first time we see this entity, create a local representation.
                    if (this.entities.Count <= state.entity_id)
                    {
                        // :: Addition (changed name entity => newEntity to avoid compile error)
                        var newEntity = new Entity();
                        newEntity.entity_id = state.entity_id;

                        if (this.entities.Count <= state.entity_id)
                        {
                            this.entities.Add(newEntity);
                        }
                        else
                        {
                            this.entities[state.entity_id] = newEntity;
                        }
                    }

                    var entity = this.entities[state.entity_id];

                    if (state.entity_id == this.entity_id)
                    {
                        // Received the authoritative position of this client's entity.
                        entity.x = state.position;

                        if (this.server_reconciliation)
                        {
                            // Server Reconciliation. Re-apply all the inputs not yet processed by
                            // the server.
                            var j = 0;
                            while (j < this.pending_inputs.Count)
                            {
                                var input = this.pending_inputs[j];
                                if (input.input_sequence_number <= state.last_processed_input)
                                {
                                    // Already processed. Its effect is already taken into account into the world update
                                    // we just got, so we can drop it.
                                    this.pending_inputs.RemoveAt(j);
                                }
                                else
                                {
                                    // Not processed by the server yet. Re-apply it.
                                    entity.ApplyInput(input);
                                    j++;
                                }
                            }
                        }
                        else
                        {
                            // Reconciliation is disabled, so drop all the saved inputs.
                            this.pending_inputs.Clear();
                        }
                    }
                    else
                    {
                        // Received the position of an entity other than this client's.

                        if (!this.entity_interpolation)
                        {
                            // Entity interpolation is disabled - just accept the server's position.
                            entity.x = state.position;
                        }
                        else
                        {
                            // Add it to the position buffer.
                            var timestamp = new Date();
                            entity.position_buffer.Add(new TimedPosition()
                            {
                                timestamp = timestamp,
                                position = state.position
                            });
                        }
                    }
                }
            }
        }

        private void InterpolateEntities()
        {
            // Compute render timestamp.
            var now = new Date();
            var render_timestamp = now - (1000d / server.update_rate);

            for (int i = 0; i < this.entities.Count; i++)
            {
                var entity = this.entities[i];

                // No point in interpolating this client's entity.
                if (entity.entity_id == this.entity_id)
                {
                    continue;
                }

                // Find the two authoritative positions surrounding the rendering timestamp.
                var buffer = entity.position_buffer;

                // Drop older positions.
                while (buffer.Count >= 2 && buffer[1].timestamp <= render_timestamp)
                {
                    buffer.RemoveAt(0);
                }

                // Interpolate between the two surrounding authoritative positions.
                if (buffer.Count >= 2 && buffer[0].timestamp <= render_timestamp && render_timestamp <= buffer[1].timestamp) // :: Addition (instead of nested array, use struct to access named fields)
                {
                    var x0 = buffer[0].position;
                    var x1 = buffer[1].position;
                    var t0 = buffer[0].timestamp;
                    var t1 = buffer[1].timestamp;

                    entity.x = (float)(x0 + (x1 - x0) * (render_timestamp - t0) / (t1 - t0));
                }
            }
        }

        private void RenderWorld()
        {
            // TODO ?
            LocalWorldCanvas.instance.RenderWorld(this.entity_id.Value + 1, this.entities);
        }
    }
}