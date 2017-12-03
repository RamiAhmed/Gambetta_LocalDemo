namespace Demo
{
    using System.Collections.Generic;

    public class Entity
    {
        public float x;
        public double speed = 10d; // units/s
        public int entity_id;
        public List<TimedPosition> position_buffer = new List<TimedPosition>();

        public void ApplyInput(Input input)
        {
            this.x += (float)(input.press_time * this.speed);
        }
    }
}