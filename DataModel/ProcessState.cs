using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataModel
{
    public enum ProcessState
    {
        Off,              // S0 - sistem oprit complet
        On_NoPump,        // Sistem pornit, nivel sub B2 - nicio pompa activa
        Pump1Running,     // Nivel atins B2 - Pompa 1 activa
        BothPumpsRunning, // Nivel atins B5 cu P1 activa - ambele pompe active
        Alarm,            // Releu protectie declansat - pompe oprite, lampa clipeste
        AlarmB3,          // Nivel critic B3 atins - pompa oprita pornita fortat + alarma
        TestPump1,        // Test manual Pompa 1 (3 secunde)
        TestPump2         // Test manual Pompa 2 (3 secunde)
    }
}
