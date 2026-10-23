# Publishing to GitHub

## 1. Create the repository
1. Go to https://github.com/new
2. Name it `clinic-appointment-system`
3. Keep it **Public**, and leave "Add a README" **unchecked** (this project already has one)
4. Click **Create repository**

## 2. Push the code

From the `clinic-appointment-system` folder:

```bash
git init
git add .
git commit -m "Initial commit: clinic appointment system with Angular and ASP.NET Core"
git branch -M main
git remote add origin https://github.com/<your-username>/clinic-appointment-system.git
git push -u origin main
```

## 3. Before you push
- Change `Jwt:Key` in `appsettings.json` to your own value, or move it to user secrets
- Run the app once and take screenshots of the booking flow, doctor schedule and admin dashboard
- Save them in `docs/screenshots/` and add them to the README

## 4. Make the repo look professional
- Add a description: "Clinic appointment booking system built with Angular, ASP.NET Core Web API, C# and SQL Server"
- Add topics: `angular`, `aspnet-core`, `csharp`, `dotnet`, `entity-framework-core`, `sql-server`, `jwt`, `full-stack`
- Pin the repository on your GitHub profile

## 5. Commit history tip
Recruiters notice commit history. As you add features, commit in small steps with clear messages, e.g.
`Add appointment cancellation`, `Add doctor schedule filters`.
