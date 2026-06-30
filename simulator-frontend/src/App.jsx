import { useState, useEffect } from 'react';
import './App.css';

const API_URL = 'http://192.168.173.129:49570/api/simulator';

const ProcessState = {
  Off: 0,
  On_NoPump: 1,
  Pump1Running: 2,
  BothPumpsRunning: 3,
  Alarm: 4,
  AlarmB3: 5,
  TestPump1: 6,
  TestPump2: 7,
};

function App() {
  const [events, setEvents] = useState([]);
  const [currentEvent, setCurrentEvent] = useState({ state: ProcessState.Off });
  const [isApiConnected, setIsApiConnected] = useState(false);
  const [currentTime, setCurrentTime] = useState(new Date().toLocaleTimeString());

  // Ceas live 
  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(new Date().toLocaleTimeString()), 1000);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const response = await fetch(API_URL);
        if (response.ok) {
          const data = await response.json();
          setIsApiConnected(true);
          if (data && data.length > 0) {
            setEvents([...data].reverse());
            
            const lastRawEvent = data[data.length - 1];
            const extractedState = typeof lastRawEvent.State !== 'undefined' 
              ? lastRawEvent.State 
              : (typeof lastRawEvent.state !== 'undefined' ? lastRawEvent.state : 0);

            setCurrentEvent({
              state: parseInt(extractedState, 10) || 0,
              stateChangedDate: lastRawEvent.StateChangedDate || lastRawEvent.stateChangedDate || new Date().toISOString()
            });
          }
        }
      } catch (err) {
        setIsApiConnected(false);
        console.error("Eroare comunicare Web API:", err);
      }
    };

    fetchData();
    const interval = setInterval(fetchData, 1000);
    return () => clearInterval(interval);
  }, []);

  // Traducerea stărilor în mesaje SCADA profesionale
  const getStateDescriptor = (stateNumber) => {
    const descriptors = {
      [ProcessState.Off]: 'Sistem Dezactivat (S0 Aus/Off)',
      [ProcessState.On_NoPump]: 'Sistem Pornit (S1 Ein) - În Așteptare Nivel',
      [ProcessState.Pump1Running]: 'Regim Normal - Pompă P3 (M1) în funcțiune',
      [ProcessState.BothPumpsRunning]: 'Debit Ridicat - Pompe P3 + P4 (M1+M2) active',
      [ProcessState.Alarm]: 'Alertă SCADA - Nivel Ridicat Rezervor',
      [ProcessState.AlarmB3]: 'AVARIE CRITICĂ - Deversare Rezervor (Senzor B3)',
      [ProcessState.TestPump1]: 'Regim Testare Manuală - Pompă P3 (M1)',
      [ProcessState.TestPump2]: 'Regim Testare Manuală - Pompă P4 (M2)',
    };
    return descriptors[stateNumber] || `Cod Stare Nedefinit (${stateNumber})`;
  };

  const state = currentEvent.state;
  
  // Mapare logică senzori din proces
  const b1 = state !== ProcessState.Off;
  const b4 = state >= ProcessState.On_NoPump && state !== ProcessState.Off;
  const b2 = state === ProcessState.Pump1Running || state === ProcessState.BothPumpsRunning || state === ProcessState.Alarm || state === ProcessState.AlarmB3 || state === ProcessState.TestPump1;
  const b5 = state === ProcessState.BothPumpsRunning || state === ProcessState.Alarm || state === ProcessState.AlarmB3;
  const b3 = state === ProcessState.Alarm || state === ProcessState.AlarmB3;

  // Stare actuatoare (Pompe)
  const isPump1Active = b2;
  const isPump2Active = state === ProcessState.BothPumpsRunning || state === ProcessState.AlarmB3 || state === ProcessState.TestPump2;
  const isAlarmTriggered = b3;

  // Calcul dinamici din istoricul Web API
  const totalAlarms = events.filter(e => {
    const s = typeof e.State !== 'undefined' ? e.State : e.state;
    return s === ProcessState.Alarm || s === ProcessState.AlarmB3;
  }).length;

  // Calcul înălțime vizuală apă în procente
  const getWaterHeight = () => {
    switch (state) {
      case ProcessState.Off: return '8%';
      case ProcessState.On_NoPump: return '45%';
      case ProcessState.Pump1Running: return '35%';
      case ProcessState.BothPumpsRunning: return '28%';
      case ProcessState.Alarm: return '78%';
      case ProcessState.AlarmB3: return '94%';
      case ProcessState.TestPump1: return '35%';
      case ProcessState.TestPump2: return '32%';
      default: return '15%';
    }
  };

  return (
    <div className="scada-container">
      {/* SECTIUNEA PRINCIPALA STÂNGA */}
      <div className="scada-viewport">
        <h2 className="scada-title">SCADA – Stație de Pompare Ape Uzate (M44)</h2>

        {/* METRICI SUPERIOARE (Păstrate conform structurii tale CSS) */}
        <div className="telemetry-summary-grid">
          <div className="summary-card">
            <span className="card-label">STARE EXECUTIVĂ PROCES (PLC)</span>
            <span className={`card-value ${isAlarmTriggered ? 'text-red' : state !== ProcessState.Off ? 'text-green' : 'text-gray'}`}>
              {getStateDescriptor(state)}
            </span>
          </div>
          <div className="summary-card text-center">
            <span className="card-label">CONEXIUNE SERVER WEB API</span>
            <span className={`status-badge ${isApiConnected ? 'badge-green' : 'badge-red'}`}>
              {isApiConnected ? "ONLINE" : "OFFLINE"}
            </span>
          </div>
          <div className="summary-card text-center">
            <span className="card-label">AVARII ÎNREGISTRATE</span>
            <span className="card-value text-orange">{totalAlarms}</span>
          </div>
        </div>

        {/* RECIPIENT GRAPHIC CANVAS */}
        <div className="canvas">
          <div className="pipe-inlet"><span className="label-u1">Conducta admisie</span><div className="valve"></div></div>
          
          <div className="tank">
            <div className="water" style={{ height: getWaterHeight() }}><div className="water-surface"></div></div>
            <div className="level-gauge"></div>
            
            <div className={`sensor label-b3 ${b3 ? 'active' : ''}`}>
              <span>B3 (Overflow)</span><div className="sensor-node red"></div>
              {b3 && <span className="crit-text">▲ DEVERSARE</span>}
            </div>
            <div className={`sensor label-b5 ${b5 ? 'active' : ''}`}>
              <span>B5 (Nivel Maxim)</span> <div className="sensor-node orange"></div>
            </div>
            <div className={`sensor label-b2 ${b2 ? 'active' : ''}`}>
              <span>B2 (Nivel Nominal)</span> <div className="sensor-node green"></div>
            </div>
            <div className="sensor label-b4"><span>B4 (Nivel Minim)</span> <div className="sensor-node gray"></div></div>
            <div className={`sensor label-b1 ${b1 ? 'active' : ''}`}>
              <span>B1 (Bază Golire)</span><div className="sensor-node blue"></div>
            </div>
          </div>

          <div className="pumps-row">
            <div className="pump-container">
              <div className={`led-indicator ${isPump1Active ? 'on' : ''}`}></div>
              <span className="pump-label">Grup P3 (M1)</span>
              <div className="pump-circle">
                <span className="pump-text">M1</span>
                <div className={`pump-blades ${isPump1Active ? 'running' : ''}`}></div>
              </div>
            </div>

            <div className="pump-container">
              <div className={`led-indicator ${isPump2Active ? 'on' : ''}`}></div>
              <span className="pump-label">Grup P4 (M2)</span>
              <div className="pump-circle">
                <span className="pump-text">M2</span>
                <div className={`pump-blades ${isPump2Active ? 'running' : ''}`}></div>
              </div>
            </div>
          </div>
          <div className="pipe-outlet"></div>
        </div>

        {/* TABEL evenetimente*/}
        <div className="historical-table-container">
          <h4 className="section-title">Registru Istoric Evenimente (Preluat din Web API /api/simulator)</h4>
          <div className="table-wrapper">
            <table className="scada-table">
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Cod Diagnostic</th>
                  <th>Descriptor Stare</th>
                </tr>
              </thead>
              <tbody>
                {events.slice(0, 6).map((event, idx) => {
                  const evState = typeof event.State !== 'undefined' ? event.State : event.state;
                  const evDate = event.StateChangedDate || event.stateChangedDate;
                  const isAlarmRow = evState === ProcessState.Alarm || evState === ProcessState.AlarmB3;
                  return (
                    <tr key={idx} className={isAlarmRow ? 'tr-alarm' : ''}>
                      <td>{evDate ? new Date(evDate).toLocaleTimeString() : 'N/A'}</td>
                      <td><span className="badge-code">0x0{evState}</span></td>
                      <td style={{ color: isAlarmRow ? '#ef4444' : '#cbd5e1' }}>
                        {getStateDescriptor(evState)}
                      </td>
                    </tr>
                  );
                })}
                {events.length === 0 && (
                  <tr>
                    <td colSpan="3" style={{ textAlign: 'center', color: '#64748b', padding: '15px' }}>
                      Se așteaptă scrierea primelor pachete telemetrice în Web API...
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* PANOUL DE CONTROL DREAPTA */}
      <div className="scada-panel">
        <h3>Dispecerat Monitorizare</h3>
        <span className="system-time">Oră Stație: {currentTime}</span>
        <hr style={{ borderColor: '#2e3748', margin: '10px 0 15px 0' }} />
        
        <div className="panel-section led-grid">
          <h4>Stare Martori Matrice I/O</h4>
          <div className="led-row"><div className={`led ${state === ProcessState.Off ? 'active-gray' : ''}`}></div> <span>[S0] Sistem Stopat</span></div>
          <div className="led-row"><div className={`led ${state !== ProcessState.Off ? 'active-green' : ''}`}></div> <span>[S1] Sistem Automat Activ</span></div>
          <div className="led-row"><div className={`led ${isPump1Active ? 'active-green' : ''}`}></div> <span>[H3] Feedback Rulare Pompă 1</span></div>
          <div className="led-row"><div className={`led ${isPump2Active ? 'active-green' : ''}`}></div> <span>[H4] Feedback Rulare Pompă 2</span></div>
        </div>

        <div className="panel-section info-box">
          <h4>Parametri Specificație Rețea</h4>
          <p><strong>Protocol Comunicare:</strong> REST HTTP Polling</p>
          <p><strong>Frecvență Actualizare:</strong> 1000 ms (1 Hz)</p>
          
        </div>
      </div>
    </div>
  );
}

export default App;