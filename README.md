# 🌊 Simulator Sistem de Pompe pentru Ape Uzate (M44)

> Aplicație desktop de simulare industrială dezvoltată în C# (WPF), ce implementează logica de control pentru un bazin de colectare deservit de două pompe și module de configurare analogică.

---

## Detalii Tehnice Application-Level
* **Framework:** .NET / WPF (Windows Presentation Foundation)
* **Arhitectură:** MVVM (Model-View-ViewModel) cu interfața `INotifyPropertyChanged` pentru actualizarea grafică în timp real.
* **Concurență:** `BackgroundWorker` asincron pentru bucla principală de calcul (loop la fiecare 100ms) și timere asincrone (`System.Timers.Timer`) pentru gestionarea stărilor temporizate și a alarmelor.

---

## Logica de Control și Funcționalități

### 🔹 Regim Digital (Funcționalitatea 1)
* **Sistem Secvențial:** Activarea simulării din butonul `S1` și oprirea completă din `S0`. Lampa `P1` reflectă starea globală a sistemului.
* **Automatul de Stări (ProcessState):** * Când nivelul apei (`WaterLevel`) atinge pragul fix `LevelB2` (100px), pornește Pompa 1.
  * Când nivelul scade sub `LevelB1` (30px), Pompa 1 se oprește.
  * Dacă rata de admisie e mare și nivelul atinge `LevelB5` (180px), pornește și Pompa 2. Ambele se opresc când se atinge nivelul minim.
* **Sistemul de Redundanță și Siguranță:**
  * Cuplarea senzorului virtual `B4` în caz de defect pe `B1` pentru a preveni rularea pompelor în gol.
  * Starea critică `B3` (Preaplin) pornește forțat ambele pompe și menține alarma activă timp de 5 secunde.
  * Declanșarea avariilor simulează clipirea lămpilor la o frecvență de $0.5\text{Hz}$.

### 🔸 Regim Analogic (Funcționalitatea 2)
* **Configurare Setpoints din UI:** Pragurile fixe sunt înlocuite de variabile memorate dinamic (`_savedLevelB2` și `_savedLevelB5`) prin citirea tensiunii simulate din Potențiometrul 2 ($U_2$).
* **Evenimente de Memorare (S1 + S3 / S1 + S4):**
  * La detectarea combinată a flag-urilor de butoane apăsate, metoda `CheckAndSaveAnalogThresholds()` transformă tensiunea curentă ($0 - 10\text{V}$) în coordonate de pixeli ($0 - 260\text{px}$) și repoziționează dinamic liniile grafice pe Canvas.
* **Modul de Admisie Dinamic ($U_1$):** Debitul de intrare nu mai este o constantă fixă, ci este calculat direct în funcție de proprietatea `PotentiometerVoltage1`.

---

## Managementul Datelor în ViewModel (Proprietăți și Binding-uri)

### State Control (Proprietăți booleene legate prin DataBinding)
| Proprietate C# | Tip | Control UI (XAML) | Descriere |
| :--- | :---: | :---: | :--- |
| `IsSystemOn` | `bool` | Indicator ON / Lampa P1 | Starea generală de funcționare a simulatorului. |
| `IsPump1Running` | `bool` | Lampă P3 / Motor M1 | Indicator activ când Pompa 1 evacuează fluid. |
| `IsPump2Running` | `bool` | Lampă P4 / Motor M2 | Indicator activ când Pompa 2 evacuează fluid. |
| `IsAlarmOn` | `bool` | Indicator Vizual Alarmă | Declanșat la depășirea pragului critic B3 sau la avarie. |
| `IsAnalogModeActive`| `bool` | CheckBox / Border Mod | Comută între limitele fixe (Digital) și cele din $U_2$ (Analog). |

### Semnale și Mapări Numerice (0-10V vs Pixeli Bazin)
| Proprietate C# | Tip | Mapare Interfață | Descriere / Rol în Simulator |
| :--- | :---: | :---: | :--- |
| `WaterLevel` | `double` | Înălțime Dreptunghi Apă | Nivelul curent din bazin, limitat strict între $0$ și $260\text{px}$. |
| `WaterTop` | `double` | `Canvas.Top` Dreptunghi | Recalculat automat ($400 - \text{WaterLevel}$) pentru randarea corectă. |
| `PotentiometerVoltage1`| `double` | Slider / Unghi Slider 1 | Tensiune virtuală ($0 - 10\text{V}$) ce dictează rata de umplere a bazinului. |
| `PotentiometerVoltage2`| `double` | Slider / Unghi Slider 2 | Tensiune virtuală ($0 - 10\text{V}$) folosită ca referință pentru setpoint-uri. |
| `AnalogOutput1_WaterLevelVoltage` | `double` | Proprietate calculată | Exportă valoarea curentă a apei convertită în plajă de tensiune ($0-10\text{V}$). |
| `AnalogOutput2_SetPointVoltage`   | `double` | Proprietate calculată | Exportă pragul activ curent (B2 sau B5 în funcție de stare) în plajă $0-10\text{V}$. |

---

## Algoritm de Transpunere Semnal în Simulator

Pentru a simula comportamentul unor echipamente industriale reale direct în codul C#, conversia dintre nivelul grafic al apei (exprimat în pixeli) și mărimile normalizate de tensiune folosește următoarea ecuație liniară implementată în proprietățile de tip Read-Only:

$$\text{AnalogOutput} = \frac{\text{WaterLevel}}{260.0} \times 10.0$$
