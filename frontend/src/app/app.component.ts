import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NavbarComponent } from './shared/navbar/navbar.component';
import { ToastsComponent } from './shared/toasts/toasts.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NavbarComponent, ToastsComponent],
  template: `
    <app-navbar />
    <main class="page">
      <router-outlet />
    </main>
    <app-toasts />
  `
})
export class AppComponent {}
