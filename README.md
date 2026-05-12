# Sistem-Pompe-Ape-Uzate-M44

Proiect de automatizare pentru controlul a două pompe de descărcare.


## Detalii Tehnice
* **Software:** (ex: TIA Portal V17 / CODESYS V3.5)
* **Hardware:** (ex: Siemens S7-1200 / Simulator)

## Funcționalități
- Pornire/Oprire sistem (S1/S0).
- Control automat prin senzori (B1-B5).
- Mod de lucru Analogic (Scalare 0-10V).
- Protecție termică și redundanță senzori.

### Tabel Alocare I/O (Intrări/Ieșiri)

| Device | Adresă PLC | Tip | Descriere |
|:---:|:---:|:---:|:---|
| **S0** | %I0.0 | DI | Buton STOP (Sistem OFF) |
| **S1** | %I0.1 | DI | Buton START (Sistem ON) |
| **B1** | %I0.2 | DI | Senzor Nivel Minim (Oprire Pompe) |
| **B2** | %I0.3 | DI | Senzor Nivel Pornire Pompa 1 |
| **B5** | %I0.4 | DI | Senzor Nivel Pornire Pompa 2 |
| **M1** | %Q0.0 | DO | Contactar Pompă 1 |
| **M2** | %Q0.1 | DO | Contactar Pompă 2 |
| **U1** | %IW64 | AI | Nivel curent (0-10V -> 0-100%) |
