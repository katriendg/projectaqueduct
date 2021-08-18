using System.Collections.Generic;

namespace ProjectAqueduct.Functions.Model
{
    public class TwinMessage
    {
        public string modelId { get; set; }

        public IList<Patch> patch { get; set; }
    }

    public class TwinMessageExt : TwinMessage
    {
        public string adtInstance { get; set; }
        public string twinId { get; set; }
    }
}