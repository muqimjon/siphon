import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Auth } from '../../services/auth';
import { I18n } from '../../services/i18n';

@Component({
  selector: 'sp-account-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './account-shell.html',
  styleUrl: './account-shell.css',
})
export class AccountShell {
  readonly i18n = inject(I18n);
  readonly auth = inject(Auth);

  readonly initial = () => (this.auth.user()?.email ?? '?').charAt(0).toUpperCase();
}
