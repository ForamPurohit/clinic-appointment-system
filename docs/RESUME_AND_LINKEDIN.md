# Resume and LinkedIn content

Replace `<your-username>` with your GitHub username.

---

## Resume entry

**Clinic Appointment System** | Angular, ASP.NET Core Web API, C#, EF Core, SQL Server
GitHub: github.com/<your-username>/clinic-appointment-system

- Built a full-stack appointment booking platform with three roles (patient, doctor, admin) using
  Angular 18 and an ASP.NET Core 8 REST API secured with JWT and role-based authorization.
- Designed a slot-generation engine that builds bookable times from each doctor's working days,
  hours, slot length and leave, and prevented double booking with service-level checks plus a
  filtered unique index in SQL Server to handle concurrent requests.
- Added real-time availability with SignalR (per-doctor groups, JWT over WebSockets), so slots taken
  by one patient disappear for everyone else instantly and doctors see new bookings live.
- Built background email notifications (confirmations, 24-hour reminders, review requests) using a
  Channel-based queue, a hosted worker with retries, and a PeriodicTimer reminder job.
- Implemented rescheduling with per-patient limits, doctor leave management, and a 1 to 5 star
  review system with privacy-safe reviewer names.
- Modelled 5 related entities with Entity Framework Core code-first migrations, and added global
  exception middleware for consistent API error responses.
- Wrote 380 automated tests: 233 xUnit unit and integration tests (WebApplicationFactory, a real
  SignalR client, EF Core InMemory, fake `TimeProvider`) and 147 Jasmine tests with 91% frontend
  coverage, run by GitHub Actions CI on every push, plus 84 documented test cases traced to them.
- Developed a responsive Angular UI with standalone components, signals, lazy-loaded routes,
  route guards and an HTTP interceptor for token handling.

Shorter version (2 lines, if space is tight):

- Built a clinic appointment system (Angular, ASP.NET Core, C#, SQL Server, SignalR) with JWT
  role-based access, real-time slot availability, email reminders, rescheduling and doctor reviews.

---

## LinkedIn "Projects" section

**Title:** Clinic Appointment System

**Description:**
A full-stack web app for booking and managing doctor appointments. Patients choose a specialty,
doctor and open time slot, reschedule or cancel online, get email confirmations and reminders, and
rate their visits. Doctors see their schedule update live as patients book and can block time off.
Admins manage doctors, specialties and all bookings from a dashboard.

Built with Angular 18 on the frontend and an ASP.NET Core 8 Web API in C#, using Entity Framework
Core with SQL Server and JWT authentication. Highlights include slot generation from doctors'
working hours and leave, double-booking prevention at both the service and database level,
real-time updates with SignalR, background email processing, role-based authorization, and 380
automated tests (xUnit and Jasmine) run in GitHub Actions CI. The repository also documents 19 use
cases traced to their tests.

**Skills:** Angular, ASP.NET Core, C#, Entity Framework Core, SQL Server, SignalR, REST APIs, JWT, TypeScript, Background Services, xUnit, Jasmine, GitHub Actions

---

## LinkedIn post (to share the project)

I just finished building a Clinic Appointment System, a full-stack project using Angular and
ASP.NET Core.

What it does:
✅ Patients book appointments by specialty, doctor and open time slot
✅ Doctors see their daily schedule and mark visits completed with notes
✅ Admins manage doctors, specialties and every booking from a dashboard
✅ Live updates: when one patient books a slot, it disappears for everyone else instantly
✅ Email confirmations, 24-hour reminders, rescheduling and star ratings

What I learned along the way:
🔹 Preventing double bookings when two people click the same slot at once. I handled it in the
   service layer and with a filtered unique index in SQL Server as a safety net.
🔹 Generating time slots from each doctor's working days, hours and slot length.
🔹 Role-based security with JWT across the API and Angular route guards.
🔹 Making time-based logic testable with .NET 8's TimeProvider.
🔹 Real-time updates with SignalR groups, and keeping emails off the request path with a background queue.

Tech: Angular 18, ASP.NET Core 8 Web API, C#, Entity Framework Core, SQL Server, SignalR, JWT, xUnit

Code on GitHub: github.com/<your-username>/clinic-appointment-system

I'd love feedback from other developers!

#Angular #DotNet #CSharp #ASPNETCore #SignalR #FullStackDeveloper #WebDevelopment #SQLServer

---

## Interview talking points

1. **How do you stop two patients booking the same slot?**
   The service checks for an existing non-cancelled appointment first. If two requests pass that
   check at the same moment, the filtered unique index on `(DoctorId, StartTime)` rejects the second
   insert, and the service turns the `DbUpdateException` into a 409 Conflict.

2. **Why a filtered index instead of a plain unique index?**
   A cancelled appointment should free the slot. The filter `Status <> 'Cancelled'` lets a new
   booking reuse that time.

3. **How is authorization handled?**
   JWT tokens carry the user's role. Controllers use `[Authorize(Roles = ...)]`, and services also
   check ownership, so a patient can't cancel someone else's appointment by guessing an ID.

4. **How did you test time-dependent logic?**
   Services take a `TimeProvider`. Tests inject `FakeTimeProvider` fixed to a known Monday morning,
   so rules like "can't book in the past" are deterministic.

5. **How do live updates work?**
   The Angular app opens one SignalR connection and calls `WatchDoctor(id)` to join a group for that
   doctor. After any booking, move, cancellation or leave change, the API sends `SlotsChanged` with the
   affected date to that group, and the page reloads just that day. Doctors join a private group
   automatically when they connect with their JWT.

6. **Why put emails on a queue instead of sending them in the request?**
   SMTP can be slow or down. The request writes to an in-memory `Channel`, returns immediately, and a
   hosted `BackgroundService` sends with retries. A failed email never fails the booking. In a larger
   system the same interface could be backed by a durable queue such as Azure Service Bus.

7. **How do you make sure a reminder is only sent once?**
   The job selects scheduled appointments in the next 24 hours where `ReminderSentAt` is null, queues
   the email, and sets `ReminderSentAt` in the same save. Rescheduling clears it so the new time gets
   its own reminder.
