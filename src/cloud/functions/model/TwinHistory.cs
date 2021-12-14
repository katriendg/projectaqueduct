using System;

namespace ProjectAqueduct.Functions.Model
{
    public class TwinHistory: TwinMessage
    {
        public string twinId { get; set; }
        public DateTime twinTime { get; set; }
        public DateTime originalTime { get; set; }
        
    }

}