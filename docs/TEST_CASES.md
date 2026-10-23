# Test Cases: New Features

Detailed test cases for the five features added in version 2: **rescheduling**, **doctor leave**,
**live updates**, **email notifications** and **ratings and reviews**. Each case can be run by hand in
a browser, and the last column names the automated test that checks the same behaviour, so every
case is verified on every push by GitHub Actions.

For the original features (booking, sign-in, admin pages), see the manual test cases in
[TESTING.md](TESTING.md#manual-test-cases). Use cases and business rules (UC-xx, BR-xx) are defined in
[USE_CASES.md](USE_CASES.md).

## Contents

1. [How to use this document](#how-to-use-this-document)
2. [Rescheduling (UC-14)](#1-rescheduling-uc-14)
3. [Doctor leave (UC-15)](#2-doctor-leave-uc-15)
4. [Ratings and reviews (UC-16, UC-17)](#3-ratings-and-reviews-uc-16-uc-17)
5. [Live updates (UC-18)](#4-live-updates-uc-18)
6. [Email notifications and reminders (UC-19)](#5-email-notifications-and-reminders-uc-19)
7. [Security and access across new features](#6-security-and-access-across-new-features)
8. [Summary](#summary)

---

## How to use this document

### Test environment

| Item | Setting |
|---|---|
| API | `dotnet run --launch-profile http` (http://localhost:5000), SQL Server, freshly seeded database |
| Frontend | `npm start` (http://localhost:4200) |
| Browsers | Chrome or Edge. Live update cases need two or three browser windows (use a private window for a second user) |
| Emails | `Email:SmtpHost` left empty, so emails are saved as `.eml` files in `backend/ClinicAppointment.API/emails/` |
| Faster reminders | For EM cases, set `Reminders:CheckIntervalMinutes` to `1` in `appsettings.Development.json` |

### Test accounts

| Role | Email | Password |
|---|---|---|
| Patient | patient@clinic.com | Patient@123 |
| Doctor (General Medicine, Mon to Fri 09:00 to 13:00, 30-minute slots) | doctor@clinic.com | Doctor@123 |
| Doctor (Cardiology, Mon/Wed/Fri/Sat 14:00 to 18:00, 20-minute slots) | cardio@clinic.com | Doctor@123 |
| Admin | admin@clinic.com | Admin@123 |

Some cases need a second patient. Register one on the **Create account** page, for example
`patient2@test.com` / `Patient@123`.

### Columns

- **Type:** *Positive* (the feature works), *Negative* (invalid input is refused), *Edge* (boundaries and unusual situations), *Security* (access rules).
- **Priority:** *High* must pass before release; *Medium* should pass; *Low* is nice to have.
- **Automated by:** the test that checks the same behaviour. Backend tests are in `backend/ClinicAppointment.Tests`, Angular specs end in `.spec.ts`. *Manual only* means the behaviour depends on a real browser or real timing.

### Recording results

Copy the tables into your test log and add **Actual result**, **Status** (Pass / Fail / Blocked),
**Tested by** and **Date** columns.

---

## 1. Rescheduling (UC-14)

**Common preconditions:** Signed in as `patient@clinic.com`. The patient has an upcoming scheduled
appointment with Dr. Priya Sharma (book one first if needed), referred to below as *the visit*.

| ID | Scenario | Steps | Expected result | Type | Priority | Automated by |
|---|---|---|---|---|---|---|
| TC-RS-01 | Move a visit to another open time | 1. Open **My appointments**.<br>2. On the visit, select **Reschedule**.<br>3. Pick an open time on another day.<br>4. Select **Move appointment**. | Returns to **My appointments** with a "Moved to ..." message. The visit shows the new date and time and "Moved once". | Positive | High | `Reschedule_ToOpenSlot_MovesTimesAndCounts`, `Patient_ReschedulesBooking_OldSlotOpensAndNewSlotIsTaken`, `RescheduleComponent: moves the appointment...` |
| TC-RS-02 | Page shows current and new time | 1. Open **Reschedule** on the visit.<br>2. Pick a new time. | The summary shows **Currently** with the old time, an arrow, and **New time** with the chosen time. Before picking, New time reads "Pick below". | Positive | Medium | `RescheduleComponent: keeps Move disabled until a new time is picked` |
| TC-RS-03 | Current time is marked and can't be chosen | 1. Open **Reschedule**.<br>2. Select the visit's current day. | The current time shows "current" with a dashed blue outline and can't be selected. It isn't counted in "x of y times open". | Edge | Medium | `SlotPickerComponent: marks the appointment being rescheduled as "current"...` |
| TC-RS-04 | Move to the slot right next to the current one | Move a 09:00 visit to 09:30 on the same day. | Move succeeds. The visit isn't blocked by its own current time. | Edge | Medium | `Reschedule_ToAdjacentSlot_IsNotBlockedByItsOwnCurrentTime` |
| TC-RS-05 | Old time becomes free | 1. Move the visit (TC-RS-01).<br>2. As a second patient, open booking for the same doctor and the old day. | The old time is open and can be booked. The new time is struck through. | Positive | High | `Reschedule_FreesTheOldSlotForOthers` |
| TC-RS-06 | Move button disabled until a time is picked | Open **Reschedule** without picking a time. | **Move appointment** is disabled. **Keep current time** returns to the list without changes. | Negative | Medium | `RescheduleComponent: keeps Move disabled until a new time is picked` |
| TC-RS-07 | Third move is refused | 1. Move the visit twice.<br>2. Return to **My appointments**. | **Reschedule** no longer appears on the visit. Opening `/reschedule/{id}` directly shows "already been moved twice. Cancel it and book a new time instead." | Negative | High | `Reschedule_PatientLimit_BlocksThirdMove`, `MyAppointmentsComponent: offers Reschedule until...`, `RescheduleComponent: explains when the patient has used up their moves` |
| TC-RS-08 | Last-change warning | Open **Reschedule** on a visit moved once. | Page says "This is your last change." | Edge | Low | `RescheduleComponent: loads the appointment...` |
| TC-RS-09 | Someone takes the new time first | 1. Patient A opens **Reschedule** and selects a time but doesn't confirm.<br>2. Patient B books that exact time.<br>3. Patient A selects **Move appointment**. | Patient A's selection is cleared as soon as B books (live update). If A still submits, "This time slot was just booked. Pick another slot." appears and the times refresh. | Edge | High | `Reschedule_ToTakenSlot_ThrowsConflict`, `RescheduleComponent: shows the API error and refreshes times`, `SlotPickerComponent: reloads the shown day...drops a slot that was taken` |
| TC-RS-10 | Can't move onto a leave day | 1. Doctor adds leave for a future day (TC-LV-01).<br>2. Patient opens **Reschedule**. | The leave day shows "Leave" and can't be selected. The API refuses it if called directly ("The doctor is on leave that day"). | Negative | High | `Reschedule_OntoDoctorsLeave_ThrowsBadRequest` |
| TC-RS-11 | Can't move to a past, closed or non-working time | Call `PATCH /api/appointments/{id}/reschedule` in Swagger with: (a) yesterday 09:00, (b) Saturday 09:00, (c) 15:00 on a weekday. | Each returns **400** with a reason. | Negative | Medium | `Reschedule_ToInvalidTime_ThrowsBadRequest` (3 cases) |
| TC-RS-12 | Can't move to the same time | Call the reschedule endpoint with the visit's current start time. | **400** "Pick a different time from the current one." | Negative | Low | `Reschedule_ToSameTime_ThrowsBadRequest` |
| TC-RS-13 | Can't move onto the patient's other appointment | 1. Book the patient with the cardiologist at time T.<br>2. Try to move the General Medicine visit to time T. | Refused: "You already have an appointment that overlaps this time." | Negative | Medium | `Reschedule_OverlappingPatientsOtherAppointment_ThrowsConflict` |
| TC-RS-14 | Can't move a visit that has started | Open **Reschedule** on a visit whose start time has passed (use the API with a past booking). | "This appointment has already started and can't be moved." | Edge | Medium | `Reschedule_AppointmentAlreadyStarted_ThrowsBadRequest` |
| TC-RS-15 | Can't move a cancelled or completed visit | Open `/reschedule/{id}` for a cancelled and for a completed visit. | Page says it's cancelled/completed and can't be moved. No picker is shown. | Negative | Medium | `Reschedule_CancelledAppointment_ThrowsBadRequest`, `RescheduleComponent: refuses appointments that are no longer scheduled` |
| TC-RS-16 | Admin moves a visit | 1. Sign in as admin.<br>2. **Appointments** → **Move** on a scheduled visit.<br>3. Pick a time and confirm. | Moves successfully and returns to **All appointments**. Works even if the patient used both moves. The patient's move count doesn't increase. | Positive | High | `Reschedule_ByAdmin_IgnoresPatientLimitAndDoesNotCount`, `RescheduleComponent: lets an admin move it regardless...`, `AdminAppointmentsComponent: offers Move...` |
| TC-RS-17 | Reschedule resets the reminder | 1. Book a visit for tomorrow and wait for the reminder email (TC-EM-04).<br>2. Move it to the day after.<br>3. Wait until it's within 24 hours. | A second reminder arrives for the new time. | Edge | Medium | `Reschedule_ResetsReminderSoTheNewTimeGetsOne` |

---

## 2. Doctor leave (UC-15)

**Common preconditions:** Signed in as `doctor@clinic.com`, on **Time off**. Choose dates within the
next two weeks so you can see them on the booking page.

| ID | Scenario | Steps | Expected result | Type | Priority | Automated by |
|---|---|---|---|---|---|---|
| TC-LV-01 | Add leave for one day | 1. Choose a weekday with no bookings as First day and Last day.<br>2. Enter reason "Conference".<br>3. Select **Add leave**. | The leave appears under **Upcoming leave** as "1 day, Conference". The form clears. | Positive | High | `Add_ValidPeriod_SavesAndRaisesEvent`, `Add_SingleDay_IsAllowed`, `LeaveComponent: adds leave...` |
| TC-LV-02 | Add a multi-day period | Add leave from Monday to Wednesday next week (no bookings). | Shows "3 days". All three days show **Leave** on the booking page. | Positive | High | `LeaveComponent: lists upcoming leave with its length` |
| TC-LV-03 | Leave day is blocked for patients | 1. Add leave (TC-LV-01).<br>2. As a patient, open booking for Dr. Priya Sharma. | The day shows "Leave" with hatched shading and can't be selected. If it was the first open day, the next working day opens instead. | Positive | High | `Slots_OnLeaveDay_AreEmpty_AndOtherDaysUnaffected`, `Doctor_AddsLeave_DayDisappearsFromBooking`, `SlotPickerComponent: marks leave days...` |
| TC-LV-04 | Booking on a leave day is refused by the API | In Swagger, as a patient, `POST /api/appointments` for 09:00 on the leave day. | **400** "The doctor is on leave that day. Pick another date." | Negative | High | `Book_OnLeaveDay_ThrowsBadRequest` |
| TC-LV-05 | Leave over booked days is refused | 1. As a patient, book a time on a future day.<br>2. As the doctor, add leave covering that day. | Refused: "You have 1 scheduled appointment in this period. Ask the clinic admin to move or cancel them first." | Negative | High | `Add_WhenAppointmentsAreBooked_ThrowsConflictWithCount`, `LeaveComponent: shows the conflict...` |
| TC-LV-06 | Cancelled bookings don't block leave | 1. Cancel the booking from TC-LV-05.<br>2. Add the same leave again. | Leave is added. | Edge | Medium | `Add_CancelledAppointmentsInPeriod_DoNotBlock` |
| TC-LV-07 | Last day before first day | Set First day after Last day by typing dates, then **Add leave**. | "The last day can't be before the first day." No request is sent. | Negative | Medium | `Add_EndBeforeStart_ThrowsBadRequest`, `LeaveComponent: validates the dates...` |
| TC-LV-08 | Last day follows first day | Choose a First day later than the current Last day. | Last day moves to match First day automatically. | Edge | Low | `LeaveComponent: moves the last day forward...` |
| TC-LV-09 | Leave in the past | The date pickers block past dates. In Swagger, post leave starting yesterday. | **400** "Leave can't start in the past." | Negative | Medium | `Add_InThePast_ThrowsBadRequest` |
| TC-LV-10 | Leave starting today | Add leave for today only (no bookings today). | Leave is added. Today's remaining times disappear for patients. | Edge | Medium | `Add_StartingToday_IsAllowed` |
| TC-LV-11 | Leave longer than 60 days | In Swagger, post leave of 61 days. | **400** "A single leave period can be at most 60 days." | Negative | Low | `Add_LongerThanLimit_ThrowsBadRequest` |
| TC-LV-12 | Overlapping leave | 1. Add leave Mon to Wed.<br>2. Add leave Wed to Fri. | Second request refused: "This overlaps leave you've already added." | Negative | Medium | `Add_OverlappingExistingLeave_ThrowsConflict` |
| TC-LV-13 | Remove leave | Select **Remove** on a leave entry and confirm. | The entry disappears. The days are bookable again (patients see this without reloading). | Positive | High | `Delete_OwnLeave_RemovesItAndRaisesEvent`, `LeaveComponent: removes leave after confirmation` |
| TC-LV-14 | Finished leave is hidden | Add leave directly in the database for last week, then open **Time off**. | Only current and upcoming leave is listed. Ongoing leave (started yesterday, ends tomorrow) is shown. | Edge | Low | `GetUpcoming_HidesFinishedLeave_SortedByStart` |
| TC-LV-15 | A doctor sees only their own leave | 1. Add leave as doctor@clinic.com.<br>2. Sign in as cardio@clinic.com and open **Time off**. | The cardiologist's list doesn't include the other doctor's leave. | Security | Medium | `GetMine_ReturnsOnlyTheSignedInDoctorsLeave` |
| TC-LV-16 | A doctor can't remove another doctor's leave | As cardio@clinic.com, call `DELETE /api/doctors/me/leaves/{id}` with doctor@clinic.com's leave id. | **404** "Leave not found." The leave remains. | Security | High | `Delete_AnotherDoctorsLeave_ThrowsNotFound` |
| TC-LV-17 | Patients can't manage leave | As a patient, call `POST /api/doctors/me/leaves`. | **403 Forbidden**. **Time off** isn't in the patient menu. | Security | High | `Patient_CannotAddLeave_Returns403`, `NavbarComponent: shows doctor links` |

---

## 3. Ratings and reviews (UC-16, UC-17)

**Common preconditions:** A completed visit for `patient@clinic.com` within the last 30 days. To create
one: book a visit for later today, wait until its start time, then as the doctor select
**Complete visit** → **Mark completed** on **My schedule**.

| ID | Scenario | Steps | Expected result | Type | Priority | Automated by |
|---|---|---|---|---|---|---|
| TC-RV-01 | Rate a completed visit | 1. As the patient, **My appointments** → **Past and cancelled**.<br>2. Select **Rate this visit**.<br>3. Choose 4 stars, type "Very thorough", select **Submit review**. | The form closes and the visit shows "You rated ★★★★☆". | Positive | High | `Create_ForOwnCompletedVisit_SavesReview`, `MyAppointmentsComponent: submits a review...` |
| TC-RV-02 | Rating is required | Open the review form and select **Submit review** without choosing stars. | "Choose from 1 to 5 stars." No request is sent. | Negative | High | `MyAppointmentsComponent: requires a star rating before submitting` |
| TC-RV-03 | Comment is optional | Submit 5 stars with no comment. | Review is saved without a comment. | Positive | Medium | `ReviewServiceTests.Create_ShowsOnTheAppointment` |
| TC-RV-04 | Postpone a review | Open the form, then select **Not now**. | Form closes. **Rate this visit** is still offered. | Positive | Low | Manual only |
| TC-RV-05 | One review per visit | After TC-RV-01, call `POST /api/appointments/{id}/review` again. | **409** "You've already reviewed this visit." **Rate this visit** no longer appears. | Negative | High | `Create_Twice_ThrowsConflict`, `CompletedVisit_CanBeReviewedOnce_AndShowsOnDoctorProfile` |
| TC-RV-06 | Only completed visits can be rated | Check a scheduled, a cancelled and a no-show visit. Call the review endpoint for each. | No **Rate this visit** button. The API returns **400** "You can review a visit once the doctor has marked it completed." | Negative | High | `Create_ForVisitNotCompleted_ThrowsBadRequest` (3 cases), `MyAppointmentsComponent: offers a rating for recent completed visits...` |
| TC-RV-07 | Rating out of range | Call the review endpoint with rating 0 and with 9. | **400** "Rating must be between 1 and 5 stars." | Negative | Medium | `Create_RatingOutOfRange_ThrowsBadRequest` (2 cases), `Review_WithInvalidRating_Returns400` |
| TC-RV-08 | Review window closes after 30 days | Change a completed visit's date to 31 days ago in the database, then reload. | No **Rate this visit** button. The API refuses: "Reviews can be left up to 30 days after a visit." | Edge | Medium | `Create_AfterReviewWindow_ThrowsBadRequest` |
| TC-RV-09 | Can't rate someone else's visit | As a second patient, call the review endpoint for the first patient's visit. | **403** "You can only review your own visits." | Security | High | `Create_ForSomeoneElsesVisit_ThrowsForbidden` |
| TC-RV-10 | Doctors and admins can't post reviews | As the doctor, call `POST /api/appointments/{id}/review`. | **403 Forbidden**. | Security | Medium | Manual only (enforced by `[Authorize(Roles = Patient)]` on `AddReview`) |
| TC-RV-11 | Rating shows on the doctor card | As any patient, open **Book a visit** → General Medicine. | Dr. Priya Sharma shows stars, the average to one decimal (for example "4.5") and the review count. A doctor with no reviews shows "New to Lakeview, no reviews yet". | Positive | High | `DoctorProfile_IncludesRoundedAverageRating`, `BookAppointmentComponent: loads doctors...with their ratings` |
| TC-RV-12 | Read reviews | Select **Read reviews** on a doctor card. | Up to 5 latest reviews appear with stars, "First name Initial., Month Year" and comment. Selecting it doesn't select the doctor. **Hide reviews** closes the list. | Positive | High | `GetForDoctor_ReturnsAverageCountAndNewestFirst`, `GetForDoctor_Take_LimitsListButNotTheCount`, `BookAppointmentComponent: shows reviews on request...` |
| TC-RV-13 | Reviewer privacy | Review as "Rahul Verma", then read reviews as another patient. | Shown as "Rahul V.". No full name, email or phone anywhere in the page or API response. | Security | High | `PublicName_HidesSurname` (4 cases) |
| TC-RV-14 | Average updates | Add a 2-star review to a doctor with one 5-star review. | Average shows 3.5 with 2 reviews on the doctor card, the doctor's **My schedule** header and the admin **Doctors** table. | Positive | Medium | `GetForDoctor_ReturnsAverageCountAndNewestFirst` |
| TC-RV-15 | Screen reader labels | Inspect the star rating with a screen reader or dev tools. | Reads "Rated 4.5 out of 5, 2 reviews" (or "1 review"), not a row of symbols. | Edge | Low | `StarsComponent` spec (3 cases) |
| TC-RV-16 | Review link in email opens the right tab | Open the "How was your visit?" email (TC-EM-03) and select **Rate your visit**. | **My appointments** opens on **Past and cancelled**. | Positive | Medium | `MyAppointmentsComponent opened from a review email` |

---

## 4. Live updates (UC-18)

**Common preconditions:** API and frontend running. Open separate browser windows as described in
each case. Don't reload any page during the test.

| ID | Scenario | Steps | Expected result | Type | Priority | Automated by |
|---|---|---|---|---|---|---|
| TC-RT-01 | Taken slot disappears for others | 1. Window A: patient 1 on **Book a visit**, Dr. Priya Sharma, a future day.<br>2. Window B: patient 2 books an open time on the same day. | Within about a second, window A shows that time struck through, the open count drops, and "Updated just now" appears. | Positive | High | `SlotWatcher_IsToldWhenSomeoneBooks`, `SlotPickerComponent: reloads the shown day...` |
| TC-RT-02 | Picked slot is cleared if taken | 1. Window A selects a time (step 4 is showing).<br>2. Window B books that time. | Window A's selection clears and the confirm step closes, so patient 1 can't submit a taken time. | Edge | High | `SlotPickerComponent: reloads the shown day when another patient books, and drops a slot that was taken` |
| TC-RT-03 | Cancellation frees the slot live | Window B cancels the booking from TC-RT-01. | Window A shows the time as open again. | Positive | High | `AppointmentEventsTests.StatusChanged_ToCancelled_ActsLikeACancellation` |
| TC-RT-04 | Changes on another day don't disturb the view | Window A views Tuesday. Window B books on Thursday. | Window A doesn't reload or show "Updated just now". | Edge | Medium | `SlotPickerComponent: ignores live changes for other days` |
| TC-RT-05 | Leave appears live | Window A: patient on booking. Window B: doctor adds leave for a visible day. | The day changes to **Leave** in window A. If it was selected, its times disappear. | Positive | Medium | `LeaveChanged_RefreshesEveryDayInThePeriod` |
| TC-RT-06 | Reschedule updates both days | Window A shows day X. Window B moves a visit from day X to day Y. | Day X shows the old time freed in window A. Switching window A to day Y shows the new time taken. | Positive | Medium | `Rescheduled_ToAnotherDay_RefreshesBothDays` |
| TC-RT-07 | Doctor sees new booking | Window A: doctor on **My schedule** for today. Window B: patient books today. | A dark message "New booking: Name, date time" appears bottom-right for about 6 seconds. The list and summary update. | Positive | High | `Doctor_ReceivesScheduleChangeForNewBooking`, `ScheduleComponent: announces a new booking...`, `ToastsComponent` spec |
| TC-RT-08 | Doctor sees cancellations and moves | Patient cancels, then moves another visit, while the doctor watches. | Messages "Cancelled: ..." and "... moved their visit to ...". The list updates if the change affects the day shown, including a visit moved away from it. | Positive | Medium | `ScheduleComponent: announces cancellations`, `ScheduleComponent: reloads when a patient moves a visit away...` |
| TC-RT-09 | Doctor only gets their own changes | Window A: doctor@clinic.com on **My schedule**. Window B: patient books the cardiologist. | No message for doctor@clinic.com. | Security | High | `Booked_NotifiesSlotWatchersAndDoctor_AndEmailsPatient` (checks the doctor-specific group) |
| TC-RT-10 | Toasts can be dismissed | Select × on a message. | It disappears at once. At most 3 messages show at a time. | Edge | Low | `ToastsComponent: shows each toast and removes it when dismissed`, `ToastService: keeps at most three toasts` |
| TC-RT-11 | Reconnects after the API restarts | 1. Window A on booking.<br>2. Stop and restart the API.<br>3. Window B books a time. | After a few seconds, window A reconnects and receives the update. | Edge | Medium | Manual only |
| TC-RT-12 | App still works without live updates | Block `/hubs/clinic` in dev tools (Network → Block request URL), then book and cancel. | Everything works. Pages just don't refresh by themselves. A warning appears only in the browser console. | Edge | High | `HubFailure_IsSwallowed_AndEmailStillQueued` (server side); manual for the browser |
| TC-RT-13 | Sign-out closes the connection | Sign out as the doctor and sign in as a patient in the same window. | The patient never receives the doctor's schedule messages. | Security | Medium | `AuthService: runs sign-out hooks...` |
| TC-RT-14 | One connection for several views | Open booking, pick a doctor, switch doctors several times. | Dev tools → Network → WS shows a single connection. | Edge | Low | `RealtimeService: connects once per doctor however many components watch it` |

---

## 5. Email notifications and reminders (UC-19)

**Common preconditions:** No SMTP host configured, so each email is saved as a `.eml` file in
`backend/ClinicAppointment.API/emails/`. Open the files in Outlook, Thunderbird or a browser
extension. Empty the folder before each case.

| ID | Scenario | Steps | Expected result | Type | Priority | Automated by |
|---|---|---|---|---|---|---|
| TC-EM-01 | Booking confirmation | Book a visit as the patient. | One email to the patient: subject "Appointment confirmed: {date, time}", with when, doctor, specialty, reason, and a **View my appointments** button. | Positive | High | `Booking_SendsConfirmationEmail`, `Booked_NotifiesSlotWatchersAndDoctor_AndEmailsPatient`, `EmailTemplatesTests.Booked_ContainsTimeDoctorAndLink` |
| TC-EM-02 | Moved and cancelled emails | Move a visit, then cancel it. | "Appointment moved to ..." with the old time struck through, then "Appointment cancelled: ..." with a **Book a new appointment** button. | Positive | High | `Rescheduled_ToAnotherDay_RefreshesBothDays`, `Rescheduled_ShowsOldTimeStruckThrough`, `StatusChanged_ToCancelled_ActsLikeACancellation` |
| TC-EM-03 | Review request after completion | Doctor marks a visit **Completed**. | Patient gets "How was your visit with Dr. ...?" with a **Rate your visit** button. | Positive | Medium | `StatusChanged_ToCompleted_AsksForReview` |
| TC-EM-04 | Reminder 24 hours before | 1. Set `CheckIntervalMinutes` to 1 and restart the API.<br>2. Book a visit for tomorrow, earlier in the day than now.<br>3. Wait 1 to 2 minutes. | A "Reminder: appointment ..." email arrives. | Positive | High | `Run_QueuesRemindersForAppointmentsInsideTheWindow` |
| TC-EM-05 | Reminder sent only once | After TC-EM-04, wait several more minutes, then restart the API. | No second reminder for the same visit. | Edge | High | `Run_Twice_SendsEachReminderOnlyOnce` |
| TC-EM-06 | No reminder for far-off visits | Book a visit 3 days ahead. | No reminder until it's within 24 hours. | Edge | Medium | `Run_PicksUpAppointmentsAsTimeMovesOn`, `Run_RespectsConfiguredWindow` |
| TC-EM-07 | No reminder for cancelled visits | Book for tomorrow, cancel it, wait for the job. | No reminder. | Negative | Medium | `Run_SkipsCancelledAndPastAppointments` |
| TC-EM-08 | No email for no-shows | Doctor marks a visit **No-show**. | No email. | Negative | Low | `StatusChanged_ToNoShow_SendsNoEmail` |
| TC-EM-09 | Booking isn't slowed by email | Configure an unreachable SMTP host (for example `SmtpHost: "10.255.255.1"`), restart, and book. | The booking confirms immediately. The API log shows retry warnings ("attempt 1/3") and the booking is still saved. | Edge | High | `RetriesAfterAFailure`, `HubFailure_IsSwallowed_AndEmailStillQueued` |
| TC-EM-10 | Patient text is safe in emails | Book with reason `<b>Bold</b><script>alert(1)</script>`. | The email shows the text literally; nothing is bold and no script runs. | Security | High | `UserText_IsHtmlEncoded` |
| TC-EM-11 | Development mode saves files | With `SmtpHost` empty, book a visit. | An `.eml` file appears in `emails/`, with the clinic as sender and the patient as recipient. | Positive | Medium | `NoSmtpHost_CreatesPickupFolderAndWritesEmlFile`, `EachEmail_GetsItsOwnFile` |
| TC-EM-12 | Dates are readable in any locale | Run the API on a machine set to another region (for example en-IN). | Email times read like "Tue 20 Oct 2026, 9:30 AM". | Edge | Low | `Booked_ContainsTimeDoctorAndLink` (formats with invariant culture) |
| TC-EM-13 | Real SMTP delivery | Set SMTP settings for a test inbox (for example Mailtrap) and book. | The email arrives in the inbox with the same content. | Positive | Low | Manual only |

---

## 6. Security and access across new features

| ID | Scenario | Steps | Expected result | Type | Priority | Automated by |
|---|---|---|---|---|---|---|
| TC-SC-01 | Patient can't reschedule another patient's visit | As patient 2, call `PATCH /api/appointments/{patient 1's id}/reschedule`. | **403** "You don't have access to this appointment." | Security | High | `Reschedule_AnotherPatientsAppointment_ThrowsForbidden` |
| TC-SC-02 | Doctor can't reschedule | As a doctor, call the reschedule endpoint for one of their own visits. | **403**. | Security | High | `Reschedule_ByDoctor_ThrowsForbidden`, `Doctor_CannotReschedule_Returns403` |
| TC-SC-03 | Signed-out user can't open the reschedule page | Sign out and open `http://localhost:4200/reschedule/1`. | Redirected to sign-in, then back to the page after signing in. | Security | Medium | Route guard specs (`roleGuard`) |
| TC-SC-04 | Doctor can't open the reschedule page | As a doctor, open `/reschedule/1`. | Redirected to **My schedule**. | Security | Medium | Route guard specs (`roleGuard`) |
| TC-SC-05 | Patient can't open Time off | As a patient, open `/doctor/leave`. | Redirected to **My appointments**. | Security | Medium | Route guard specs (`roleGuard`) |
| TC-SC-06 | Public endpoints reveal nothing private | Signed out, call `GET /api/doctors/{id}/leaves` and `GET /api/doctors/{id}/reviews`. | Both return **200**. Leave shows dates and reason only; reviews show "First name Initial." only. No emails, phone numbers or patient IDs. | Security | High | `PublicName_HidesSurname`, `Doctor_AddsLeave_DayDisappearsFromBooking` |
| TC-SC-07 | Hub doesn't leak schedules | Connect to `/hubs/clinic` without a token (for example from the browser console) and call `WatchDoctor(1)`. | Receives only `SlotsChanged` (doctor ID and date). Never `ScheduleChanged`, which contains patient details. | Security | High | `SlotWatcher_IsToldWhenSomeoneBooks`, `Doctor_ReceivesScheduleChangeForNewBooking` |

---

## Summary

| Area | Test cases | High priority | Automated | Manual only |
|---|---|---|---|---|
| Rescheduling | 17 | 6 | 17 | 0 |
| Doctor leave | 17 | 8 | 17 | 0 |
| Ratings and reviews | 16 | 8 | 14 | 2 |
| Live updates | 14 | 6 | 13 | 1 |
| Emails and reminders | 13 | 6 | 12 | 1 |
| Security and access | 7 | 4 | 7 | 0 |
| **Total** | **84** | **38** | **80** | **4** |

The 4 manual-only cases (TC-RV-04, TC-RV-10, TC-RT-11, TC-EM-13) depend on a real
browser, a server restart, a real mail server, or an attribute check with no dedicated test. Run them before each release.
