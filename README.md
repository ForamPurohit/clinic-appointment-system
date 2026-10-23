# Clinic Appointment System

A full-stack web application for booking and managing doctor appointments at a clinic.
Patients book open time slots, doctors manage their daily schedule, and admins run the clinic:
doctors, specialties, patients and every appointment.

**Tech stack:** Angular 18, ASP.NET Core 8 Web API, C#, Entity Framework Core, SQL Server, SignalR, JWT, xUnit, Jasmine

> Add screenshots to a `docs/screenshots` folder and link them here, e.g.
> `![Booking flow](docs/screenshots/booking.png)`

---

## Features

### Patient
- Register and sign in with JWT authentication
- Book in four steps: specialty, doctor, day, open time slot
- Only the doctor's working days are selectable; taken slots are shown and blocked
- View upcoming and past appointments, read the doctor's visit notes
- **Reschedule** an upcoming visit to another open time (up to twice), or cancel it
- **Rate completed visits** (1 to 5 stars with a comment) and read other patients' reviews when choosing a doctor
- **Email confirmations, reminders** 24 hours before, and notices when a visit is moved or cancelled
- **Live availability:** times taken by other patients disappear from the screen instantly

### Doctor
- Day-by-day schedule with patient details and reason for visit
- **Live updates:** new bookings, moves and cancellations appear with a notification, no refresh needed
- Mark visits as **Completed** (with notes), **No-show** or **Cancelled**
- **Time off:** block holidays or conferences so those days can't be booked
- See their average patient rating

### Admin
- Dashboard with today's visits and clinic statistics
- Add and edit doctors: specialty, fee, working days, hours and slot length
- Deactivate doctors who leave (blocks new bookings and sign-in)
- Manage specialties and view all patients
- Filter all appointments by status, doctor and date, and move or cancel any of them
- See every doctor's rating

### Engineering highlights
- **Double-booking prevention at two levels:** service-layer validation plus a filtered unique
  index in SQL Server (`DoctorId + StartTime WHERE Status <> 'Cancelled'`) that catches race conditions
- **Slot generation** from each doctor's working hours, slot length, working days and leave
- **Real-time updates with SignalR:** group-based broadcasting (per-doctor slot watchers, private
  doctor schedule channels), JWT authentication over WebSockets, automatic reconnect
- **Background processing:** an email queue built on `System.Threading.Channels` with a hosted worker
  and retries, plus a reminder job on a `PeriodicTimer` that sends each reminder exactly once
- **Event-driven design:** services raise `IAppointmentEvents`; one handler fans out to SignalR and
  email, so business logic stays free of delivery details and notification failures never break a request
- **Role-based authorization** (`Admin`, `Doctor`, `Patient`) on the API and route guards in Angular
- **Global exception middleware** returning consistent `{ status, message }` JSON errors
- **380 automated tests:** xUnit unit and `WebApplicationFactory` integration tests (including a real
  SignalR client) for the API, Jasmine/Karma tests for Angular (91% statement coverage), run by
  GitHub Actions on every push
- **Testable time:** services use .NET 8 `TimeProvider`, so tests run against a fixed clock
- **Modern Angular:** standalone components, signals, new control flow (`@if`, `@for`),
  functional guards and HTTP interceptor, lazy-loaded routes
- Swagger UI with JWT support for trying the API

---

## Project structure

```
clinic-appointment-system/
├── backend/
│   ├── ClinicAppointment.sln
│   ├── ClinicAppointment.API/
│   │   ├── Controllers/        # Auth, Doctors, Appointments, Specializations, Admin
│   │   ├── Data/               # AppDbContext, DbSeeder
│   │   ├── DTOs/               # Request/response contracts
│   │   ├── Exceptions/         # AppException + NotFound, Conflict, Forbidden...
│   │   ├── Hubs/               # ClinicHub (SignalR)
│   │   ├── Notifications/      # events, email queue + worker, templates, reminder job
│   │   ├── Extensions/         # ClaimsPrincipal helpers
│   │   ├── Middleware/         # ExceptionMiddleware
│   │   ├── Models/             # User, Doctor, Patient, Specialization, Appointment
│   │   ├── Services/           # Business logic (booking rules, slots, tokens)
│   │   └── Program.cs
│   └── ClinicAppointment.Tests/
│       ├── Services/           # unit tests for each service
│       ├── Notifications/      # SignalR messages, emails, reminders
│       ├── Middleware/         # error handling tests
│       └── Integration/        # HTTP tests through the real API pipeline
└── frontend/
    └── src/app/
        ├── core/               # models, services, guards, interceptor
        ├── shared/             # navbar, slot picker, stars, toasts, status badge
        └── features/
            ├── home/
            ├── auth/           # login, register
            ├── patient/        # book-appointment, my-appointments, reschedule
            ├── doctor/         # schedule, leave
            └── admin/          # dashboard, doctors, specializations, appointments, patients
```

## Documentation

- [Use cases](docs/USE_CASES.md): actors, use case diagram, 19 detailed use cases and 26 business rules
- [Testing](docs/TESTING.md): strategy, how to run, traceability matrix and manual test cases
- [Test cases](docs/TEST_CASES.md): 84 detailed test cases for rescheduling, leave, live updates, emails and reviews

## Database design

| Table | Key columns |
|---|---|
| Users | Id, FullName, Email (unique), PasswordHash, Role, PhoneNumber |
| Doctors | Id, UserId → Users, SpecializationId → Specializations, ConsultationFee, WorkStartTime, WorkEndTime, SlotDurationMinutes, WorkingDays, IsActive |
| Patients | Id, UserId → Users, DateOfBirth, Gender, Address |
| Specializations | Id, Name (unique), Description |
| Appointments | Id, DoctorId → Doctors, PatientId → Patients, StartTime, EndTime, Reason, Notes, Status, RescheduleCount, ReminderSentAt |
| DoctorLeaves | Id, DoctorId → Doctors, StartDate, EndDate, Reason |
| Reviews | Id, AppointmentId → Appointments (unique), DoctorId, PatientId, Rating (1 to 5, check constraint), Comment |

---

## Getting started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18.19+ or 20+](https://nodejs.org/) and npm
- SQL Server or SQL Server Express LocalDB (installed with Visual Studio)
- EF Core CLI: `dotnet tool install --global dotnet-ef`

### 1. Run the API

```bash
cd backend/ClinicAppointment.API

# Set your connection string and a JWT secret in appsettings.json first (see Configuration)

dotnet restore
dotnet ef migrations add InitialCreate
dotnet run --launch-profile http
```

> **Upgrading from an earlier version?** If you already created `InitialCreate`, add a migration for
> the new tables instead: `dotnet ef migrations add AddLeaveReviewsReminders`. To start fresh with the
> new demo data (including a sample review), run `dotnet ef database drop` first.

On first start the API applies migrations and seeds demo data.
Swagger opens at **http://localhost:5000/swagger**.

### 2. Run the Angular app

```bash
cd frontend
npm install
npm start
```

Open **http://localhost:4200**.

### 3. Run the tests

```bash
cd backend && dotnet test          # 233 API unit and integration tests
cd frontend && npm run test:ci     # 147 Angular tests with coverage
```

See [docs/TESTING.md](docs/TESTING.md) for the test strategy, coverage and manual test cases.

### Demo accounts

| Role | Email | Password |
|---|---|---|
| Admin | admin@clinic.com | Admin@123 |
| Doctor | doctor@clinic.com | Doctor@123 |
| Doctor | cardio@clinic.com | Doctor@123 |
| Patient | patient@clinic.com | Patient@123 |

---

## Configuration

`backend/ClinicAppointment.API/appsettings.json`

| Setting | Description |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection. Default uses LocalDB. For SQL Express use `Server=.\\SQLEXPRESS;Database=ClinicAppointmentDb;Trusted_Connection=True;TrustServerCertificate=True` |
| `Jwt:Key` | Secret used to sign tokens. **Use a random string of 32+ characters.** |
| `Jwt:ExpiryMinutes` | Token lifetime |
| `Cors:AllowedOrigins` | Frontend URLs allowed to call the API (also used for the SignalR connection) |
| `Email:SmtpHost` | Leave empty in development: emails are saved as `.eml` files in `ClinicAppointment.API/emails/`. Set it (with `SmtpPort`, `Username`, `Password`) to send real email, for example through Gmail, SendGrid or Mailtrap |
| `Email:ClientUrl` | Address of the Angular app, used for links in emails |
| `Reminders:HoursBefore` / `CheckIntervalMinutes` | When reminders are sent, and how often the job checks (default 24 hours / 15 minutes) |

For real secrets, use [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) instead of committing them:

```bash
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "your-long-random-secret"
```

The Angular app reads the API URL from `frontend/src/environments/environment.ts`.

---

## API endpoints

| Method | Endpoint | Access | Description |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Register a patient |
| POST | `/api/auth/login` | Public | Sign in, returns JWT |
| GET | `/api/auth/me` | Signed in | Current user |
| GET | `/api/specializations` | Public | List specialties |
| POST / PUT / DELETE | `/api/specializations/{id}` | Admin | Manage specialties |
| GET | `/api/doctors?specializationId=` | Public | List active doctors |
| GET | `/api/doctors/{id}/slots?date=yyyy-MM-dd` | Public | Time slots with availability |
| GET | `/api/doctors/{id}/leaves` | Public | Current and upcoming leave |
| GET | `/api/doctors/{id}/reviews?take=` | Public | Average rating and latest reviews |
| GET | `/api/doctors/me` | Doctor | Own profile |
| GET / POST | `/api/doctors/me/leaves` | Doctor | List or add own leave |
| DELETE | `/api/doctors/me/leaves/{leaveId}` | Doctor | Remove own leave |
| POST / PUT | `/api/doctors/{id}` | Admin | Add or update a doctor |
| POST | `/api/appointments` | Patient | Book an appointment |
| GET | `/api/appointments/mine` | Patient | Own appointments |
| GET | `/api/appointments/schedule?date=` | Doctor | Own schedule |
| GET | `/api/appointments?status=&doctorId=&date=` | Admin | All appointments |
| PATCH | `/api/appointments/{id}/reschedule` | Patient (owner) / Admin | Move to another open slot |
| POST | `/api/appointments/{id}/review` | Patient (owner) | Rate a completed visit |
| PATCH | `/api/appointments/{id}/cancel` | Owner / Admin | Cancel |
| PATCH | `/api/appointments/{id}/status` | Doctor / Admin | Complete, no-show or cancel |
| GET | `/api/admin/stats` | Admin | Dashboard numbers |
| GET | `/api/admin/patients?search=` | Admin | Patient list |
| WS | `/hubs/clinic` | Public; doctors send their token | SignalR hub: `WatchDoctor(id)`, `SlotsChanged`, `ScheduleChanged` |

---

## Future improvements
- SMS reminders (for example through Twilio) alongside email
- Calendar invites (.ics) attached to confirmation emails
- Waiting list that offers newly freed slots to patients
- Docker Compose setup and deployment to Azure

## License
MIT
