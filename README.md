<div dir="rtl" align="right">

# KeyFlip

اصلاح خودکار زبان تایپ در ویندوز: اگر با کیبورد اشتباه تایپ کنید (مثلاً `hdk` به‌جای «این» یا «فاهس» به‌جای `this`)، KeyFlip کلمه را به زبان درست تبدیل می‌کند و در صورت نیاز زبان کیبورد را هم عوض می‌کند.

## امکانات

- تبدیل خودکار متن از انگلیسی به فارسی و برعکس، بعد از زدن Space.
- پنج حالت: **خاموش**، **انگلیسی**، **فارسی**، **خودکار** (دوطرفه) و **دستی**.
- حالت **دستی**: با یک میانبر (پیش‌فرض `Ctrl + Alt + Space`) متن انتخاب‌شده تبدیل می‌شود؛ اگر چیزی انتخاب نشده باشد، کل متن فیلد فعلی تبدیل می‌شود.
- گزینه «تغییر زبان کیبورد»: اگر خاموش باشد فقط متن عوض می‌شود و زبان کیبورد دست‌نخورده می‌ماند.
- اجرا در System Tray (کنار ساعت) با پنجره تنظیمات کوچک و مینیمال.
- گزینه اجرا همراه با روشن شدن ویندوز.
- یک فایل `KeyFlip.exe` مستقل؛ نیازی به نصب .NET نیست.

## نصب و استفاده

1. آخرین نسخه `KeyFlip.exe` را از بخش [Releases](../../releases) دانلود کنید.
2. فایل را اجرا کنید. آیکون برنامه در System Tray ظاهر می‌شود (ممکن است داخل فلش «نمایش آیکون‌های مخفی» باشد).
3. روی آیکون کلیک کنید و حالت دلخواه را انتخاب کنید.
4. در حالت خودکار/انگلیسی/فارسی، بعد از Space کلمه اشتباه اصلاح می‌شود. در حالت دستی، میانبر را بزنید.

> برای کار کردن برنامه، هر دو چیدمان انگلیسی و فارسی باید در ویندوز نصب باشند.

## حالت دستی

1. متنی را انتخاب کنید (یا هیچ‌چیز انتخاب نکنید تا کل متن فیلد تبدیل شود).
2. میانبر را بزنید.
3. برنامه متن را کپی، تبدیل و جایگزین می‌کند، سپس کلیپ‌بورد قبلی (فقط متن) را برمی‌گرداند.

جهت تبدیل از روی خود متن تعیین می‌شود: اگر بیشتر حروف لاتین باشد به فارسی و در غیر این صورت به انگلیسی تبدیل می‌شود.

برای تغییر میانبر، فایل `%AppData%\KeyFlip\settings.json` را ویرایش کنید: `HotkeyModifiers` (Alt=1، Ctrl=2، Shift=4، Win=8، جمع می‌شوند) و `HotkeyKey` (کد کلید مجازی ویندوز به‌صورت عدد دهدهی؛ Space = 32).

## روش تشخیص (حالت خودکار)

کلمه فقط وقتی تبدیل می‌شود که در زبان فعلی شناخته‌شده نباشد و یکی از این دو شرط برقرار باشد:

1. نسخه تبدیل‌شده در دیکشنری زبان دیگر وجود داشته باشد.
2. کلمه حداقل ۴ حرف داشته باشد و امتیاز مدل n-gram حرفی (سه‌حرفی) برای زبان دیگر به‌طور محسوس بالاتر باشد.

دیکشنری‌ها هنگام ساخت پروژه با `tools/prepare_data.py` از منابع عمومی ساخته می‌شوند.

## محدودیت‌ها

- در حالت‌های خودکار، تبدیل فقط هنگام زدن Space انجام می‌شود (نه Enter یا Tab).
- حالت دستی از کلیپ‌بورد استفاده می‌کند و فقط متن قبلی آن بازگردانده می‌شود (تصویر و فایل کپی‌شده از بین می‌رود).
- در ویرایشگرهایی که Ctrl+C بدون انتخاب، کل خط را کپی می‌کنند (مثل بعضی IDEها)، حالت دستی بدون انتخاب فقط همان خط را تبدیل می‌کند.
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

## منابع داده و مجوزها

- لیست کلمات انگلیسی: [dwyl/english-words](https://github.com/dwyl/english-words)
- لیست کلمات فارسی: [behnam/persian-words-frequency](https://github.com/behnam/persian-words-frequency) (CC BY-SA 3.0)
- فونت: [Vazirmatn](https://github.com/rastikerdar/vazirmatn)

</div>

---

# KeyFlip

Automatic typing-language correction for Windows. If you type with the wrong keyboard layout (for example `hdk` instead of "این", or "فاهس" instead of `this`), KeyFlip converts the word to the intended language and can switch the keyboard layout for you.

## Features

- Automatic English-to-Persian and Persian-to-English conversion after you press Space.
- Five modes: **Off**, **English**, **Persian**, **Auto** (both directions) and **Manual**.
- **Manual** mode: press a hotkey (default `Ctrl + Alt + Space`) to convert the selected text; with no selection, the whole content of the current field is converted.
- "Switch keyboard layout" option: when off, only the text is replaced and your layout stays unchanged.
- Lives in the system tray with a small, minimal settings popup.
- Optional start with Windows.
- A single standalone `KeyFlip.exe`; no .NET installation required.

## Install and use

1. Download the latest `KeyFlip.exe` from [Releases](../../releases).
2. Run it. The icon appears in the system tray (it may be under the "Show hidden icons" arrow).
3. Click the icon and pick a mode.
4. In Auto/English/Persian modes a wrong-layout word is fixed when you press Space. In Manual mode, press the hotkey.

> Both the English and Persian keyboard layouts must be installed in Windows.

## Manual mode

1. Select some text (or select nothing to convert the whole field).
2. Press the hotkey.
3. KeyFlip copies the text, converts it, pastes it over the original, then restores your previous text clipboard.

The direction is inferred from the text itself: mostly Latin letters are converted to Persian, otherwise to English.

To change the hotkey, edit `%AppData%\KeyFlip\settings.json`: `HotkeyModifiers` (Alt=1, Ctrl=2, Shift=4, Win=8, summed) and `HotkeyKey` (Windows virtual-key code in decimal; Space = 32).

## How detection works (Auto mode)

A word is converted only if it is unknown in the current language and one of these holds:

1. The converted form exists in the other language's word list.
2. The word has at least 4 letters and a character-trigram model scores the other language noticeably higher.

The word lists are generated at build time from public sources by `tools/prepare_data.py`.

## Limitations

- In the automatic modes, conversion triggers on Space only (not Enter or Tab).
- Manual mode uses the clipboard and restores text only (copied images or files are lost).
- In editors where Ctrl+C without a selection copies the whole line (some IDEs), Manual mode with no selection converts just that line.
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

## Data sources and licenses

- English word list: [dwyl/english-words](https://github.com/dwyl/english-words)
- Persian word list: [behnam/persian-words-frequency](https://github.com/behnam/persian-words-frequency) (CC BY-SA 3.0)
- Font: [Vazirmatn](https://github.com/rastikerdar/vazirmatn)

