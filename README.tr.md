<p align="center">
  <img src="docs/logo-512.png" width="128" alt="Mandal logosu">
</p>

<h1 align="center">Mandal</h1>

<p align="center">Ekran alıntıların için bir çamaşır ipi. Al, ipe as, lazım olunca ordan al.</p>

<p align="center"><a href="README.md">English</a></p>

<p align="center">
  <a href="https://github.com/ceceys/mandal/releases/latest/download/Mandal-Setup.exe"><img src="https://img.shields.io/github/v/release/ceceys/mandal?label=Windows%20i%C3%A7in%20indir&style=for-the-badge&color=2ea44f" alt="Mandal'ı Windows için indir" height="40"></a>
</p>

<p align="center">Tek tıkla kurulum, yönetici hakkı gerekmez · <a href="https://github.com/ceceys/mandal/releases/latest/download/Mandal.exe">Taşınabilir exe</a> · <a href="https://github.com/ceceys/mandal/releases/latest">Tüm dosyalar</a></p>

![Mandal](docs/promo/hero-wide-tr.png)

**Mandal**, küçük bir Windows aracı. Her alıntı ekranın üstündeki çamaşır ipine mandalla asılır. İp sen çağırana kadar görünmez; çağırınca bir karta tıklayıp kopyalarsın, başka uygulamaya sürüklersin ya da ✕ ile indirirsin.

## Özellikler

- **Alıntı al**: bölge (`Ctrl+Shift+S` veya `PrtScn`), tam ekran (`Ctrl+Shift+F`), aktif pencere (`Ctrl+Shift+W`).
- **Pano izleme**: başka bir araçla (ör. `Win+Shift+S`) kopyalanan görüntüler de asılır.
- **Tek tık kopyalar** (görüntü + dosya); **sürükle-bırak** Explorer, Word, tarayıcı ve sohbet uygulamalarında çalışır.
- **Önizleme**: karta çift tıkla, ekranın üstünde büyük açılır. Kenarlardan boyutlandır, tekerlekle yakınlaştır, raptiye düğmesiyle diğer uygulamaların üstünde tut.
- **Görseli metne çevir**: `Aa` düğmesi (Windows'un yerleşik OCR'ı, çevrimdışı). Metin kendi kartı olur.
- **Notlar**: sağ karttan not yaz; alıntı gibi ipe asılır. Büyük aç, bir kısmını seçip bırak sadece o kopyalanır; ya da tümünü kopyala.
- **Öğeye kısayol**: bir karta ör. `Ctrl+Alt+1` ata; basınca o kart kopyalanır, bilgisayar yeniden açılsa da.
- **Uzayan ip**: öğe arttıkça kartlar küçülür (ekran başına 12 → 16 → 24 → 32 → 40 → 50 → 60); fazlası oklarla veya tekerlekle kaydırılır.
- **Kalıcı**: alıntılar gün klasörlerinde PNG/TXT olarak durur, yeniden açılışta geri gelir.
- **12 dil**: Türkçe, English, Deutsch, Français, Español, Italiano, Português, Русский, العربية, 中文, 日本語, 한국어.
- **Otomatik güncelleme**: GitHub Releases'tan, SHA-256 ile doğrulanır, kurmadan önce her zaman sorar (kapatılabilir).
- **Gizli**: telemetri yok; tek ağ erişimi güncelleme denetimi. Bkz. [docs/SECURITY.md](docs/SECURITY.md).

![Mandal ayrıntıları](docs/promo/features-grid.png)

## Kurulum

1. **[Mandal-Setup.exe dosyasını indir](https://github.com/ceceys/mandal/releases/latest/download/Mandal-Setup.exe)** ve çalıştır. Yalnızca senin kullanıcın için kurulur, yönetici hakkı gerekmez, .NET çalışma ortamını içerir.
2. Windows *Windows bilgisayarınızı korudu* uyarısı verirse **Ek bilgi → Yine de çalıştır** seç. Dosya kod imzalı olmadığı için bu uyarı normal.

Taşınabilir seçenek: [Mandal.exe](https://github.com/ceceys/mandal/releases/latest/download/Mandal.exe), tek dosya, [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) gerekir. Tüm sürümler ve sürüm notları [Releases](https://github.com/ceceys/mandal/releases) sayfasında.

Windows 10 sürüm 2004 veya üstü. Mandal kendini GitHub Releases üzerinden günceller.

## İpi kullanmak

| İşlem | Nasıl |
|---|---|
| İpi göster / gizle | `Ctrl+Shift+Space`, tepsi simgesi, sol üst köşedeki küçük sekme ya da fareyi sol üst köşeye götürmek |
| Kartı kopyala | Tıkla |
| Kartı başka uygulamaya taşı | Sürükle |
| Büyük önizleme | Karta çift tıkla ya da üzerine gelip büyütece tıkla |
| Görsel → metin | Kartın üzerine gel, `Aa`'ya tıkla |
| Sil | Kartın üzerine gel, ✕'e tıkla |
| Aç, farklı kaydet, klasörde göster, kısayol ata | Karta sağ tık |
| Al / Ayarlar / Kaldır | Sağ uçtaki sabit karttaki düğmeler |

Tüm kısayollar **Ayarlar**'dan (tepsi menüsü veya sağ uçtaki kart) değiştirilebilir.

## Derleme

.NET 8 SDK gerekir.

```
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Kurulum paketi ([Inno Setup 6](https://jrsoftware.org/isinfo.php) gerekir):

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish-setup
iscc /DAppVersion=1.3.0 setup\Mandal.iss
```

## Dosyalar

- Alıntılar: `%APPDATA%\Mandal\Clips\yyyy-MM-dd\` (Ayarlar'dan değiştirilebilir)
- Ayarlar, öğe kısayolları, günlük: `%APPDATA%\Mandal\`

## Geri bildirim ve katkı

Hata bildirimleri, öneriler ve çeviri düzeltmeleri memnuniyetle karşılanır. Bir [issue](https://github.com/ceceys/mandal/issues/new/choose) aç ya da [CONTRIBUTING.md](CONTRIBUTING.md) dosyasına bak.

## Lisans

[MIT](LICENSE)
