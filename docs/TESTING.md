# Testing

The project has **380 automated test cases**: 233 for the ASP.NET Core API and 147 for the
Angular app. Every use case in [USE_CASES.md](USE_CASES.md) is covered, and the
[traceability matrix](#traceability-matrix) shows which tests verify each one.

## Contents

1. [Test strategy](#test-strategy)
2. [Running the tests](#running-the-tests)
3. [Backend tests](#backend-tests)
4. [Frontend tests](#frontend-tests)
5. [Traceability matrix](#traceability-matrix)
6. [Manual test cases](#manual-test-cases)
7. [Continuous integration](#continuous-integration)

---

## Test strategy

| Level | What it checks | Tools | Count |
|---|---|---|---|
| Backend unit tests | Business rules in each service, notifications (SignalR messages, emails, reminders), error handling middleware, JWT creation | xUnit, EF Core InMemory, `FakeTimeProvider`, hand-written fakes | 198 |
| Backend integration tests | The real HTTP pipeline: routing, model validation, JWT authentication, role authorization, JSON errors, seeding, a real SignalR client, and the email worker | `WebApplicationFactory`, SignalR client, EF Core InMemory | 35 |
| Frontend unit tests | Services, HTTP interceptor, route guards, the real-time service, and every page's behaviour | Jasmine, Karma, `HttpTestingController` | 147 |
| Manual tests | End-to-end flows in a real browser against SQL Server | Checklist below, plus 84 detailed cases for the new features in [TEST_CASES.md](TEST_CASES.md) | 22 + 84 |

### Key techniques

- **Fixed clock.** Booking rules depend on "now" (no booking in the past, completing only after a
  visit starts). Services take .NET 8's `TimeProvider`, and tests inject a `FakeTimeProvider`
  set to **Monday 19 October 2026, 08:00**. Tests can then move time forward with `SetNow(...)`.
  Angular specs do the same with `jasmine.clock().mockDate(...)`.
- **Fresh database per test.** Each backend unit test creates its own in-memory database
  through the `TestData` helper, so tests never affect each other.
- **Proving data was saved.** Where it matters, tests read results back through a *new*
  `DbContext` (`data.NewContext()`) rather than trusting the one that wrote them.
- **Real pipeline for security.** Integration tests boot the actual API, so a test for
  "a patient calling an admin endpoint gets 403" exercises the real `[Authorize]` attributes and JWT validation.
- **Random order.** Jasmine runs specs in random order to catch hidden dependencies between tests.
- **Fakes instead of a mocking library.** `RecordingEvents`, `RecordingEmailQueue`, `RecordingEmailSender`
  and `FakeHubContext` (in `Helpers/Fakes.cs`) record what the code under test tried to send.
  Services raise events through `IAppointmentEvents`, so their tests check *which* events fired
  without involving SignalR or email at all. In Angular, `FakeRealtimeService` lets specs push live events.
- **Real-time over real transport.** Two integration tests connect an actual SignalR client to the
  in-memory API and assert the message arrives when another user books.

### What the in-memory database doesn't cover

EF Core's in-memory provider ignores the filtered unique index that blocks double bookings
under concurrent requests (BR-07). The service-level check is fully tested; the database index
is covered by manual test **TC-08** against SQL Server.

---

## Running the tests

### Backend

```bash
cd backend
dotnet test
```

With a coverage report (Cobertura XML in `TestResults/`):

```bash
dotnet test --collect:"XPlat Code Coverage"
```

To turn that into an HTML report:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
```

Run one class or one test:

```bash
dotnet test --filter "FullyQualifiedName~AppointmentServiceTests"
dotnet test --filter "Name=Book_SameSlotTwice_ThrowsConflict"
```

### Frontend

```bash
cd frontend
npm test              # opens Chrome, re-runs on every change
npm run test:ci       # runs once in headless Chrome with a coverage report
```

The coverage report is written to `frontend/coverage/clinic-appointment-client/index.html`.

On Linux CI machines without Chrome on the PATH, set `CHROME_BIN` to the Chrome or Chromium executable.

---

## Backend tests

```
backend/ClinicAppointment.Tests/
├── Helpers/
│   ├── TestData.cs                         # in-memory clinic, fixed clock, builders
│   └── Fakes.cs                            # recording events, email queue/sender, fake hub context
├── Services/
│   ├── AppointmentServiceTests.cs          # 55 cases: booking, viewing, access, cancelling, outcomes, events
│   ├── DoctorServiceTests.cs               # 28 cases: listing, create/update, slot generation
│   ├── RescheduleTests.cs                  # 19 cases: moving appointments and their limits
│   ├── ReviewServiceTests.cs               # 20 cases: rating rules, averages, privacy of names
│   ├── LeaveServiceTests.cs                # 16 cases: leave rules and their effect on booking
│   ├── AuthServiceTests.cs                 # 14 cases: register, sign in, inactive doctors
│   ├── SpecializationServiceTests.cs       # 10 cases: CRUD and delete protection
│   ├── AdminServiceTests.cs                #  6 cases: dashboard stats, patient search
│   └── TokenServiceTests.cs                #  3 cases: JWT claims, expiry, signature
├── Notifications/
│   ├── AppointmentEventsTests.cs           # 11 cases: SignalR groups and messages, emails, templates
│   ├── ReminderJobTests.cs                 #  7 cases: reminder window, send-once, email retries
│   └── SmtpEmailSenderTests.cs             #  2 cases: .eml files in development mode
├── Middleware/ExceptionMiddlewareTests.cs  #  7 cases: status codes and safe error messages
└── Integration/
    ├── ApiFactory.cs                       # boots the API with in-memory DB, fixed clock, recorded emails
    ├── AppointmentEndpointsTests.cs        # 10 cases
    ├── AuthEndpointsTests.cs               #  9 cases
    ├── FeatureEndpointsTests.cs            #  9 cases: reschedule, leave, reviews, email, live SignalR
    └── AdminEndpointsTests.cs              #  7 cases
```

### Test data (`TestData`)

| Item | Details |
|---|---|
| Now | Monday 19 Oct 2026, 08:00 |
| Specialties | General Medicine (2 doctors), Cardiology (none) |
| `Doctor` | Dr. Active, Mon to Fri, 09:00 to 12:00, 30-minute slots |
| `InactiveDoctor` | Dr. Retired, same hours, deactivated |
| Patients | Pat One (`p1@test.com`), Pat Two (`p2@test.com`) |
| Admin | `admin@test.com` |
| Password | `Secret@123` for everyone |

### Naming

Tests follow `Method_Scenario_ExpectedResult`, for example
`Book_PatientAlreadyBookedWithAnotherDoctorAtSameTime_ThrowsConflict`, and use the
Arrange / Act / Assert layout with a blank line between each part.

---

## Frontend tests

| Spec | Cases | Covers |
|---|---|---|
| `core/utils.spec.ts` | 8 | Error messages, local date formatting, date maths |
| `core/services/auth.service.spec.ts` | 10 | Sign in, register, sign out and its hooks, restoring and expiring sessions |
| `core/services/api-services.spec.ts` | 12 | URLs, HTTP methods, bodies and query parameters for every API call |
| `core/services/toast-and-realtime.spec.ts` | 4 | Toast timing and limit; live events filtered per doctor, one connection per doctor |
| `core/interceptors/auth.interceptor.spec.ts` | 5 | Adds the token; signs out on 401 only when a token was sent |
| `core/guards/auth.guards.spec.ts` | 6 | Redirects for signed-out users, wrong roles, and signed-in visitors to sign-in |
| `shared/slot-picker` | 13 | Days, leave, taken and current slots, live refresh, clearing a slot someone else took |
| `shared/navbar` | 7 | Links per role, initials, sign out, mobile menu |
| `shared/stars`, `shared/status-badge` | 5 | Rating glyphs and accessible labels; status labels and colours |
| `shared/toasts` | 2 | Showing, dismissing and announcing live messages |
| `features/auth/login` | 5 | Validation, demo accounts, redirects, API errors |
| `features/auth/register` | 4 | Validation, optional fields sent as null, duplicate email |
| `features/patient/book-appointment` | 9 | The booking flow, ratings and reviews on doctor cards, conflict handling |
| `features/patient/my-appointments` | 14 | Tabs, cancelling, reschedule limits, rating a visit, opening from an email link |
| `features/patient/reschedule` | 7 | Current vs new time, moving, errors, move limits, admin access |
| `features/doctor/schedule` | 14 | Day navigation, outcomes, live booking, cancellation and move announcements |
| `features/doctor/leave` | 6 | Date validation, adding in order, conflicts, removing |
| `features/admin/doctors` | 8 | Add and edit forms, working days, validation, payloads |
| `features/admin/specializations` | 4 | Validation, create, edit, delete errors |
| `features/admin/appointments` | 4 | Filters, Move link, cancelling |

**Latest run:** 147 passed, 0 failed.

| Coverage | Result |
|---|---|
| Statements | 91.2% |
| Branches | 81.3% |
| Functions | 86.5% |
| Lines | 92.7% |

---

## Traceability matrix

How many automated test cases verify each use case. Some tests count toward more than one use case.

| Use case | Backend unit | Backend integration | Frontend | Manual |
|---|---|---|---|---|
| UC-01 Register as a patient | 4 | 3 | 4 | TC-01, TC-02 |
| UC-02 Sign in and sign out | 13 | 6 | 32 | TC-03, TC-04, TC-15 |
| UC-03 Find a doctor and open times | 13 | 1 | 8 | TC-05 |
| UC-04 Book an appointment | 20 | 5 | 4 | TC-06, TC-07, TC-08 |
| UC-05 View my appointments | 6 | 1 | 6 | TC-09 |
| UC-06 Cancel an appointment | 8 | 1 | 4 | TC-10 |
| UC-07 View daily schedule | 2 | 2 | 4 | TC-11 |
| UC-08 Record a visit outcome | 12 | 0 | 5 | TC-12 |
| UC-09 View the clinic dashboard | 2 | 3 | 0 | TC-13 |
| UC-10 Manage doctors | 12 | 2 | 8 | TC-14, TC-15 |
| UC-11 Manage specialties | 10 | 2 | 4 | TC-16 |
| UC-12 Oversee all appointments | 4 | 1 | 3 | TC-13 |
| UC-13 View patients | 4 | 2 | 0 | TC-13 |
| UC-14 Reschedule an appointment | 19 | 2 | 9 | TC-17, TC-18 |
| UC-15 Manage time off | 16 | 2 | 8 | TC-19 |
| UC-16 Rate a visit | 11 | 2 | 4 | TC-20 |
| UC-17 Read doctor reviews | 8 | 1 | 4 | TC-20 |
| UC-18 See live updates | 6 | 2 | 10 | TC-21 |
| UC-19 Receive email notifications | 16 | 1 | 0 | TC-22 |

### Business rules

| Rule | Key tests |
|---|---|
| BR-01 Unique email | `Register_ExistingEmailInDifferentCase_ThrowsConflict`, `Create_EmailAlreadyUsed_ThrowsConflict` |
| BR-02 Hashed passwords | `Register_StoresHashedPasswordNotPlainText` |
| BR-03 Same sign-in error message | `Login_UnknownEmail_ThrowsSameMessageAsWrongPassword` |
| BR-04 Future only | `Book_PastTime_ThrowsBadRequest`, `Book_ExactlyNow_ThrowsBadRequest`, `GetSlots_Today_TimesAlreadyPassedAreUnavailable` |
| BR-05 Working days | `Book_OnNonWorkingDay_ThrowsBadRequest`, `GetSlots_NonWorkingDay_ReturnsEmpty` |
| BR-06 Hours and slot boundaries | `Book_InvalidTimeOfDay_ThrowsBadRequest` (5 cases), `Book_LastSlotOfTheDay_Succeeds` |
| BR-07 One booking per slot | `Book_SameSlotTwice_ThrowsConflict`, `SecondPatient_BookingSameSlot_Returns409`, manual TC-08 |
| BR-08 No overlapping patient bookings | `Book_PatientAlreadyBookedWithAnotherDoctorAtSameTime_ThrowsConflict` |
| BR-09 Cancellation rules | `Cancel_PastAppointmentByPatient_ThrowsBadRequest`, `Cancel_PastAppointmentByStaff_IsAllowed`, `Cancel_AlreadyCancelled_ThrowsBadRequest` |
| BR-10 Outcome only after start | `UpdateStatus_BeforeAppointmentStarts_ThrowsBadRequest` |
| BR-11 Outcomes are final | `UpdateStatus_AlreadyCompleted_ThrowsBadRequest`, `Cancel_CompletedAppointment_ThrowsBadRequest` |
| BR-12 Inactive doctors | `Login_InactiveDoctor_ThrowsForbidden`, `Book_WithInactiveDoctor_ThrowsNotFound`, `GetAll_ByDefault_ExcludesInactiveDoctors` |
| BR-13 Valid working hours | `Create_InvalidWorkingHours_ThrowsBadRequest` (3), `Create_InvalidWorkingDay_ThrowsBadRequest` (2) |
| BR-14 Specialty rules | `Create_ExistingName_ThrowsConflict`, `Delete_SpecialtyWithDoctors_ThrowsBadRequest` |
| BR-15 Role access | `NonAdmin_CallingAdminEndpoints_Returns403`, `Doctor_TryingToBook_Returns403`, `UpdateStatus_ByPatient_ThrowsForbidden`, route guard specs |
| BR-16 Own appointments only | `GetById_AnotherPatient_ThrowsForbidden`, `Patient_ViewingAnotherPatientsAppointment_Returns403`, `Cancel_ByAnotherPatient_ThrowsForbidden` |
| BR-17 Reschedule rules | `Reschedule_ToTakenSlot_ThrowsConflict`, `Reschedule_ToInvalidTime_ThrowsBadRequest` (3), `Reschedule_AppointmentAlreadyStarted_ThrowsBadRequest` |
| BR-18 Move limits and roles | `Reschedule_PatientLimit_BlocksThirdMove`, `Reschedule_ByAdmin_IgnoresPatientLimitAndDoesNotCount`, `Reschedule_ByDoctor_ThrowsForbidden` |
| BR-19 No booking on leave | `Book_OnLeaveDay_ThrowsBadRequest`, `Slots_OnLeaveDay_AreEmpty_AndOtherDaysUnaffected`, `Doctor_AddsLeave_DayDisappearsFromBooking` |
| BR-20 Valid leave | `Add_EndBeforeStart_ThrowsBadRequest`, `Add_InThePast_ThrowsBadRequest`, `Add_LongerThanLimit_ThrowsBadRequest`, `Add_OverlappingExistingLeave_ThrowsConflict` |
| BR-21 No leave over bookings | `Add_WhenAppointmentsAreBooked_ThrowsConflictWithCount`, `Add_CancelledAppointmentsInPeriod_DoNotBlock` |
| BR-22 Review rules | `Create_Twice_ThrowsConflict`, `Create_ForVisitNotCompleted_ThrowsBadRequest` (3), `Create_AfterReviewWindow_ThrowsBadRequest`, `Create_RatingOutOfRange_ThrowsBadRequest` (2) |
| BR-23 Reviewer privacy | `PublicName_HidesSurname` (4) |
| BR-24 One reminder per time | `Run_Twice_SendsEachReminderOnlyOnce`, `Reschedule_ResetsReminderSoTheNewTimeGetsOne` |
| BR-25 Change emails | `Booked_NotifiesSlotWatchersAndDoctor_AndEmailsPatient`, `StatusChanged_ToCompleted_AsksForReview`, `Booking_SendsConfirmationEmail` |
| BR-26 Notifications never block | `HubFailure_IsSwallowed_AndEmailStillQueued`, `RetriesAfterAFailure` |

---

## Manual test cases

This section covers the original features. Detailed cases for rescheduling, leave, live updates,
emails and reviews (84 cases with priorities and linked automated tests) are in
[TEST_CASES.md](TEST_CASES.md).

Run these in a browser with the API connected to SQL Server and freshly seeded demo data.
Use the demo accounts from the README.

| ID | Use case | Steps | Expected result |
|---|---|---|---|
| TC-01 | UC-01 | Open **Create account**. Enter a new name, email and a 6+ character password. Select **Create account**. | You land on **Book an appointment**, signed in as a patient. |
| TC-02 | UC-01 | Register again with `patient@clinic.com`. | "An account with this email already exists." The form keeps your input. |
| TC-03 | UC-02 | Sign in with each demo account. | Patient opens **My appointments**, doctor opens **My schedule**, admin opens **Overview**. |
| TC-04 | UC-02 | Sign in with the patient email and a wrong password. | "Email or password is incorrect." |
| TC-05 | UC-03 | As a patient, open **Book a visit**. Choose General Medicine, then Dr. Priya Sharma. | Weekends are disabled. Today's past times are struck through. |
| TC-06 | UC-04 | Pick an open time, enter a reason and select **Book appointment**. | Confirmation shows the right date, time and doctor. The visit appears in **My appointments**. |
| TC-07 | UC-04 | Go back to the same doctor and day. | The time you booked is now struck through. |
| TC-08 | UC-04 | Open two browsers as two different patients. Choose the same doctor, day and time in both. Select **Book appointment** in both as close together as possible. | One succeeds. The other shows "This time slot was just booked. Pick another slot." and the times refresh. Only one row exists in the database. |
| TC-09 | UC-05 | Open **My appointments** and switch between tabs. | Upcoming shows future scheduled visits, soonest first. Past shows the rest. |
| TC-10 | UC-06 | Cancel an upcoming visit and confirm. | It moves to **Past and cancelled** as Cancelled. Its time is bookable again. |
| TC-11 | UC-07 | Sign in as the doctor. Use **Previous day**, **Next day**, the date picker and **Today**. | The list and summary change to match each day. |
| TC-12 | UC-08 | On a visit whose time has passed, select **Complete visit**, add notes, select **Mark completed**. Sign in as the patient. | The doctor sees Completed. The patient sees the notes under **Past and cancelled**. |
| TC-13 | UC-09, UC-12, UC-13 | Sign in as admin. Check **Overview**, filter **Appointments** by status and doctor, and search **Patients**. | Numbers match the data. Filters and search return only matching rows. |
| TC-14 | UC-10 | Add a doctor working only Tuesdays, 10:00 to 12:00, 20-minute slots. Sign in as them. | The doctor can sign in. Patients see only Tuesdays, with 6 slots each. |
| TC-15 | UC-10, UC-02 | Edit that doctor and clear **Accepting appointments**. Try to sign in as them. | Sign-in is refused. The doctor no longer appears in booking. |
| TC-16 | UC-11 | Add a specialty, rename it, then try to delete General Medicine. | Add and rename work. Deleting General Medicine is refused with a message to reassign doctors first. |
| TC-17 | UC-14 | As a patient, select **Reschedule** on an upcoming visit, pick a new time and select **Move appointment**. | The list shows the new time and "Moved once". A "moved" email appears in `backend/ClinicAppointment.API/emails`. The old time is bookable again. |
| TC-18 | UC-14 | Move the same visit a second time, then go back to it. | After the second move, **Reschedule** no longer appears. As admin, **Move** still works. |
| TC-19 | UC-15 | As the doctor, add leave for a day with no bookings. In another browser, open booking for that doctor. Then try adding leave over a day that has a booking. | The day shows **Leave** and can't be picked, without reloading. Leave over a booked day is refused with the number of bookings. |
| TC-20 | UC-16, UC-17 | Complete a visit as the doctor (TC-12), then as the patient select **Rate this visit**, give 4 stars and a comment. Open booking for that doctor and select **Read reviews**. | The visit shows "You rated ★★★★☆". The review appears with the patient's first name and initial, and the doctor's average updates. |
| TC-21 | UC-18 | Open the doctor's **My schedule** in one browser and the booking page for the same doctor and day in another. Book a time as a second patient in a third browser. | The doctor sees "New booking: ..." and the list updates. The other patient's page shows the time as taken and "Updated just now". No page reloads needed. |
| TC-22 | UC-19 | Book a visit for tomorrow morning. Wait up to 15 minutes (or set `Reminders:CheckIntervalMinutes` to 1). | `emails` contains a confirmation and then a reminder `.eml`. Restarting the API doesn't send the reminder again. |

---

## Continuous integration

`.github/workflows/ci.yml` runs both test suites on every push and pull request to `main`.
The results appear in the **Actions** tab of the repository, and you can add a status badge
to the README:

```markdown
![CI](https://github.com/<your-username>/clinic-appointment-system/actions/workflows/ci.yml/badge.svg)
```
