# Contributing to Mandal

Bug reports, ideas, translation fixes and pull requests are welcome. You can write in English or Turkish.

## Reporting a bug

Open an [issue](https://github.com/ceceys/mandal/issues/new/choose) and include:

- the Mandal version (Settings → About),
- your Windows version (`winver`),
- the last lines of `%APPDATA%\Mandal\mandal.log`.

Please do not attach screenshots that contain personal data.

Security problems should be reported privately through [Security → Report a vulnerability](https://github.com/ceceys/mandal/security/advisories/new), not in a public issue.

## Translations

Interface texts are in `Resources/Strings.xx.json` and release notes in `Resources/Notes.xx.md`, where `xx` is the language code. Missing texts fall back to English. To fix or improve a translation, edit the file and open a pull request.

## Building

Requires Windows and the .NET 8 SDK.

```
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

The installer needs [Inno Setup 6](https://jrsoftware.org/isinfo.php); see the README for the command.

## Pull requests

- Keep each pull request to one topic.
- Follow the existing code style and avoid new compiler or analyzer warnings.
- Describe what you changed and how you tested it.
- Leave the version number and `CHANGELOG.md` as they are; they are updated when a release is made.

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).
