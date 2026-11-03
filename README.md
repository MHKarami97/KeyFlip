<div dir="rtl" align="right">

# KeyFlip

اصلاح خودکار زبان تایپ در ویندوز: اگر با کیبورد اشتباه تایپ کنید (مثلاً `hdk` به‌جای «این» یا «فاهس» به‌جای `this`)، KeyFlip کلمه را به زبان درست تبدیل می‌کند و در صورت نیاز زبان کیبورد را هم عوض می‌کند.

## امکانات

- تبدیل خودکار متن از انگلیسی به فارسی و برعکس، بعد از زدن Space.
- پنج حالت: **خاموش**، **انگلیسی**، **فارسی**، **خودکار** (دوطرفه) و **دستی**.
- حالت **دستی**: با یک میانبر (پیش‌فرض `Ctrl + Alt + Space`) متن انتخاب‌شده تبدیل می‌شود؛ اگر چیزی انتخاب نشده باشد، کل متن فیلد فعلی تبدیل می‌شود. میانبر از داخل پنجره تنظیمات قابل تغییر است.
- **برگرداندن (Undo)**: اگر بلافاصله بعد از یک تبدیل خودکار Backspace بزنید، متن و زبان کیبورد به حالت قبل برمی‌گردد و آن کلمه دیگر تبدیل نمی‌شود.
- **یادگیری از رفتار شما**: تبدیل‌هایی که برنگردانید به‌مرور پذیرفته‌شده حساب می‌شوند.
- **برنامه‌های مستثنا**: تبدیل خودکار در برنامه‌های انتخابی (پیش‌فرض: ترمینال‌ها و مدیرهای رمز) و در فیلدهای رمز کلاسیک غیرفعال است.
- گزینه «تغییر زبان کیبورد»: اگر خاموش باشد فقط متن عوض می‌شود.
- اجرا در System Tray با پنجره تنظیمات مینیمال، منوی تیره و گزینه اجرا همراه با ویندوز.
- یک فایل `KeyFlip.exe` مستقل؛ نیازی به نصب .NET نیست.

## نصب و استفاده

1. آخرین نسخه `KeyFlip.exe` را از بخش [Releases](../../releases) دانلود کنید.
2. فایل را اجرا کنید. آیکون برنامه در System Tray ظاهر می‌شود (ممکن است داخل فلش «نمایش آیکون‌های مخفی» باشد).
3. روی آیکون کلیک کنید و حالت دلخواه را انتخاب کنید. برای گزینه‌های بیشتر، «تنظیمات پیشرفته» را باز کنید.
4. در حالت‌های خودکار/انگلیسی/فارسی، بعد از Space کلمه اشتباه اصلاح می‌شود. در حالت دستی، میانبر را بزنید.

> برای کار کردن برنامه، هر دو چیدمان انگلیسی و فارسی باید در ویندوز نصب باشند.

> در صورت خطا Unknown Publisher زمان اجرا کافی است روی Run Anyway کلیک کنید

## حالت دستی

1. متنی را انتخاب کنید (یا هیچ‌چیز انتخاب نکنید تا کل متن فیلد تبدیل شود).
2. میانبر را بزنید.
3. برنامه متن را کپی، تبدیل و جایگزین می‌کند، سپس کلیپ‌بورد قبلی (فقط متن) را برمی‌گرداند.

جهت تبدیل از روی خود متن تعیین می‌شود: اگر بیشتر حروف لاتین باشد به فارسی و در غیر این صورت به انگلیسی.

برای تغییر میانبر، روی کادر «میانبر حالت دستی» در پنجره تنظیمات کلیک کنید و ترکیب جدید را بزنید (حداقل Ctrl یا Alt لازم است).

## روش تشخیص (حالت خودکار)

برای هر کلمه که با Space تمام می‌شود، به ترتیب این مراحل اجرا می‌شود:

1. کلمه‌هایی که ساختارشان متن عادی نیست (حروف بزرگ، camelCase، چسبیده به عدد یا نماد) نادیده گرفته می‌شوند.
2. فهرست شخصی شما بررسی می‌شود: «هرگز تبدیل نشود» و «همیشه تبدیل شود».
3. اگر کلمه در زبان فعلی شناخته‌شده باشد (فهرست کلمات پربسامد به‌اضافه جداکردن پیشوند و پسوند)، دست نمی‌خورد.
4. اگر نسخه تبدیل‌شده در زبان دیگر شناخته‌شده باشد، تبدیل می‌شود.
5. در غیر این صورت، یک مدل n-gram حرفی (سه‌حرفی با هموارسازی Kneser-Ney) دو نسخه را مقایسه می‌کند. زبان کلمه‌های اخیر روی تصمیم‌های مرزی اثر دارد. اگر شواهد ضعیف باشد، کلمه معلق می‌ماند و فقط وقتی تبدیل می‌شود که کلمه بعدی هم همان جهت را تأیید کند.

ثابت‌های تشخیص (آستانه‌ها و اندازه فهرست‌ها) تجربی‌اند و در `Core/LanguageDetector.cs` قرار دارند.

## محدودیت‌ها

- در حالت‌های خودکار، تبدیل فقط هنگام زدن Space انجام می‌شود (نه Enter یا Tab).
- حروف با Shift در چیدمان فارسی و اعداد پشتیبانی نمی‌شوند؛ این کلمه‌ها تبدیل نمی‌شوند.
- حالت دستی از کلیپ‌بورد استفاده می‌کند و فقط متن قبلی آن بازگردانده می‌شود (تصویر و فایل کپی‌شده از بین می‌رود).
- در ویرایشگرهایی که Ctrl+C بدون انتخاب کل خط را کپی می‌کند (مثل بعضی IDEها)، حالت دستی بدون انتخاب فقط همان خط را تبدیل می‌کند.
- تشخیص فیلد رمز فقط برای کادرهای Edit کلاسیک ویندوز کار می‌کند، نه مرورگرها و برنامه‌های مدرن.
- نگاشت کلیدها بر اساس چیدمان استاندارد فارسی ویندوز است؛ چیدمان‌های دیگر ممکن است متفاوت باشند.
- در پنجره‌هایی که با دسترسی Administrator اجرا شده‌اند، برنامه فقط وقتی کار می‌کند که خودش هم Administrator باشد.
- دقت تشخیص برای کلمات کوتاه و اسامی خاص محدود است.

## ساخت از روی سورس

پیش‌نیاز: .NET 10 SDK و Python 3.

```
python tools/prepare_data.py
dotnet publish -c Release
```

فایل `Fonts/Vazirmatn-Regular.ttf` را قبل از ساخت در پوشه `Fonts` قرار دهید. خروجی در `bin/Release/net10.0-windows/win-x64/publish/KeyFlip.exe` است.

انتشار خودکار: با ساختن و push کردن یک تگ مثل `v1.0.0`، GitHub Actions برنامه را می‌سازد و فایل exe را به Release اضافه می‌کند.

## فایل‌های کاربر

- `%AppData%\KeyFlip\settings.json`: تنظیمات برنامه.
- `%AppData%\KeyFlip\user-words.json`: کلمه‌هایی که یاد گرفته شده‌اند.
- `%AppData%\KeyFlip\debug.log`: لاگ تشخیصی (فقط در build حالت Debug).

## مشارکت

مشارکت شما خوشحال‌مان می‌کند. راهنمای کامل در [CONTRIBUTING.md](CONTRIBUTING.md) است. خلاصه: ابتدا یک Issue باز کنید، شاخه‌ای جدا بسازید، تغییرات را در یک Pull Request کوچک و متمرکز بفرستید.

## منابع داده و مجوزها

- لیست کلمات انگلیسی: [Peter Norvig، count_1w.txt](https://norvig.com/ngrams/)
- لیست کلمات فارسی: [behnam/persian-words-frequency](https://github.com/behnam/persian-words-frequency) (CC BY-SA 3.0)
- فونت: [Vazirmatn](https://github.com/rastikerdar/vazirmatn)

کد برنامه تحت مجوز MIT است (فایل [LICENSE](LICENSE)). مجوز داده‌ها و فونت جداست؛ پیش از بازنشر فایل exe یا فایل‌های `Data/*.txt` شرایط هر منبع (از جمله «اشتراک به همان صورت» در CC BY-SA) را بررسی کنید.

</div>

<div align="center">
<img src="/docs/01.jpg" alt="key flop">
<br>
<img src="/docs/02.jpg" alt="key flop">
</div>

---

# KeyFlip

Automatic typing-language correction for Windows. If you type with the wrong keyboard layout (for example `hdk` instead of "این", or "فاهس" instead of `this`), KeyFlip converts the word to the intended language and can switch the keyboard layout for you.

## Features

- Automatic English-to-Persian and Persian-to-English conversion after you press Space.
- Five modes: **Off**, **English**, **Persian**, **Auto** (both directions) and **Manual**.
- **Manual** mode: press a hotkey (default `Ctrl + Alt + Space`) to convert the selected text; with no selection, the whole content of the current field is converted. The hotkey can be changed in the settings popup.
- **Undo**: press Backspace right after an automatic conversion to restore the original text and layout; that word will no longer be converted.
- **Learns from you**: conversions you do not undo are gradually treated as accepted.
- **Excluded apps**: automatic conversion is disabled in selected apps (default: terminals and password managers) and in classic password fields.
- "Switch keyboard layout" option: when off, only the text is replaced.
- Lives in the system tray with a minimal settings popup, a dark menu, and an optional start with Windows.
- A single standalone `KeyFlip.exe`; no .NET installation required.

## Install and use

1. Download the latest `KeyFlip.exe` from [Releases](../../releases).
2. Run it. The icon appears in the system tray (it may be under the "Show hidden icons" arrow).
3. Click the icon and pick a mode. Open "Advanced settings" for more options.
4. In Auto/English/Persian modes a wrong-layout word is fixed when you press Space. In Manual mode, press the hotkey.

> Both the English and Persian keyboard layouts must be installed in Windows.

## Manual mode

1. Select some text (or select nothing to convert the whole field).
2. Press the hotkey.
3. KeyFlip copies the text, converts it, pastes it over the original, then restores your previous text clipboard.

The direction is inferred from the text itself: mostly Latin letters are converted to Persian, otherwise to English.

To change the hotkey, click the "Manual-mode hotkey" box in the settings popup and press the new combination (at least Ctrl or Alt is required).

## How detection works (Auto mode)

For every word ended by Space, KeyFlip runs these steps in order:

1. Words that are not plain text (ALL CAPS, camelCase, glued to digits or symbols) are skipped.
2. Your personal lists are checked: "never convert" and "always convert".
3. If the word is known in the current language (frequency-ranked word list plus prefix/suffix stripping), it is left alone.
4. If the converted form is known in the other language, it is converted.
5. Otherwise a character n-gram model (trigrams with Kneser-Ney smoothing) compares both candidates. The language of recent words nudges borderline decisions. If the evidence is weak, the word is held back and converted only when the next word confirms the same direction.

The detection constants (thresholds and list sizes) are empirical and live in `Core/LanguageDetector.cs`.

## Limitations

- In the automatic modes, conversion triggers on Space only (not Enter or Tab).
- Shifted characters in the Persian layout and digits are not supported; such words are not converted.
- Manual mode uses the clipboard and restores text only (copied images or files are lost).
- In editors where Ctrl+C without a selection copies the whole line (some IDEs), Manual mode with no selection converts just that line.
- Password-field detection works only for classic Windows Edit controls, not browsers or modern apps.
- Key mapping follows the standard Windows Persian layout; other layouts may differ.
- In windows running as Administrator, KeyFlip works only if it is also run as Administrator.
- Accuracy is limited for very short words and proper nouns.

## Build from source

Requirements: .NET 10 SDK and Python 3.

```
python tools/prepare_data.py
dotnet publish -c Release
```

Put `Fonts/Vazirmatn-Regular.ttf` in the `Fonts` folder before building. The output is `bin/Release/net10.0-windows/win-x64/publish/KeyFlip.exe`.

Automated release: pushing a tag such as `v1.0.0` makes GitHub Actions build the app and attach the exe to a Release.

## User files

- `%AppData%\KeyFlip\settings.json`: app settings.
- `%AppData%\KeyFlip\user-words.json`: learned words.
- `%AppData%\KeyFlip\debug.log`: diagnostic log (Debug builds only).

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for the full guide. In short: open an issue first, work on a separate branch, and send a small, focused pull request.

## Data sources and licenses

- English word list: [Peter Norvig, count_1w.txt](https://norvig.com/ngrams/)
- Persian word list: [behnam/persian-words-frequency](https://github.com/behnam/persian-words-frequency) (CC BY-SA 3.0)
- Font: [Vazirmatn](https://github.com/rastikerdar/vazirmatn)

The application code is licensed under the MIT License (see [LICENSE](LICENSE)). The data and the font have their own licenses; before redistributing the exe or the `Data/*.txt` files, check each source's terms (including the share-alike condition of CC BY-SA).

[![Architecture diagram](https://gitdiagram.com/diagram-badge.svg)](https://gitdiagram.com/mhkarami97/keyflip?utm_source=readme&utm_medium=badge)
  
![GitHub Release](https://img.shields.io/github/v/release/mhkarami97/KeyFlip)
