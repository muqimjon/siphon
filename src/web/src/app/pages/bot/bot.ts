import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { I18n } from '../../services/i18n';

@Component({
  selector: 'sp-bot-guide',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bot.html',
  styleUrl: './bot.css',
})
export class BotGuide {
  readonly i18n = inject(I18n);
  readonly botUrl = 'https://t.me/siphon_bot';
  readonly steps = [1, 2, 3, 4, 5, 6, 7];
}
