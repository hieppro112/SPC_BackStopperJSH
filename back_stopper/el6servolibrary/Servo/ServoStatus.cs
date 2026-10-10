using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servoleadshine.Model
{
    public class ServoStatus
    {
        public bool ServoOn { get; set; }

        public bool Busy { get; set; }

        public bool Alarm { get; set; }

        public bool InPosition { get; set; }

        public bool HomeComplete { get; set; }

        public ushort RawValue { get; set; }
        
    }
}
