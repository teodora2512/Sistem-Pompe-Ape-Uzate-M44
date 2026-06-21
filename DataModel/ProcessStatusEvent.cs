using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataModel
{
    public class ProcessStatusEvent
    {
        public ProcessStatusEvent(ProcessState state, DateTime date)
        {
            State = state;
            StateChangedDate = date;
        }

        public ProcessStatusEvent() { }

        public ProcessState State { get; set; }

        public DateTime StateChangedDate { get; set; }
    }
}
