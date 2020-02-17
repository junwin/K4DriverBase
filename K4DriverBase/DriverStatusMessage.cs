using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using K4ServiceInterface;

namespace DriverBase
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DriverStatusMessage : IDriverStatusMessage
    {
        public List<IDriverSession> Sessions {get; set;}
        [JsonProperty]
        public string Text {get; set;}
        [JsonProperty]
        public string DriverCode {get; set;}
        [JsonProperty]
        public string Module  {get; set;}
        [JsonProperty]
        public int State { get; set; } 
    }
}
