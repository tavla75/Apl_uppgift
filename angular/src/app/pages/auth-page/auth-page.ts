import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-auth-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './auth-page.html',
  styleUrl: './auth-page.css',
})
export class AuthPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly isRegister = this.route.snapshot.routeConfig?.path === 'register';
  protected email = '';
  protected identifier = '';
  protected userName = '';
  protected password = '';
  protected error = '';
  protected submitting = false;

  protected submit(): void {
    this.error = '';
    this.submitting = true;
    const request = this.isRegister
      ? this.auth.register({
          email: this.email.trim(),
          userName: this.userName.trim(),
          password: this.password,
        })
      : this.auth.login({ identifier: this.identifier.trim(), password: this.password });

    request.subscribe({
      next: () => {
        this.submitting = false;
        void this.router.navigate(['/home-page']);
      },
      error: (response: HttpErrorResponse) => {
        this.submitting = false;
        this.error = response.status === 401
          ? 'Användarnamnet, e-postadressen eller lösenordet stämmer inte.'
          : response.error?.detail ??
            response.error?.title ??
            'Det gick inte att ansluta. Kontrollera att API:t är igång och försök igen.';
      },
    });
  }
}
