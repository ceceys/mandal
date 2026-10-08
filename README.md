# Mandal

Çamaşır ipi tarzı ekran alıntısı aracı (Windows 10/11). Alıntılar ekranın üstündeki ipe mandalla asılır; lazım olunca tıklayıp kopyalar ya da sürükleyip başka uygulamaya bırakırsın.

## Kısayollar (varsayılan)

| İşlev | Tuş |
|---|---|
| Bölge seç ve as | Ctrl+Shift+S veya PrtScn |
| Tam ekran (imlecin olduğu ekran) | Ctrl+Shift+F |
| Aktif pencere | Ctrl+Shift+W |
| İpi indir / kaldır | Ctrl+Shift+Space (veya tepsi simgesine tık) |

Kısayollar `%APPDATA%\Mandal\settings.json` dosyasından değiştirilir (tepsi menüsü → Ayarlar dosyasını aç). Değişiklik için uygulama yeniden başlatılır.

## İpteki alıntı

- **Tek tık**: panoya kopyalar (görüntü + dosya). İp kendiliğinden kalkar, Ctrl+V ile yapıştır.
- **Sürükle**: Explorer, Word, tarayıcı, WhatsApp gibi uygulamalara bırak.
- **Üzerine gel → kırmızı çarpı**: ipten ve diskten siler.
- **Çift tık**: varsayılan görüntüleyicide açar. **Sağ tık**: Kopyala, Aç, Farklı kaydet, Klasörde göster, Sil.
- Fare tekerleği ipi yatay kaydırır.

## Pano izleme

Açıkken (varsayılan) Win+Shift+S veya başka bir araçla panoya düşen her görüntü otomatik ipe asılır. Tepsi menüsünden kapatılabilir.

## Dosyalar

- Alıntılar: `Resimler\Mandal\yyyy-MM-dd_HH-mm-ss.png`
- Ayarlar ve günlük: `%APPDATA%\Mandal\`

## Derleme

.NET 8 SDK gerekir.

```
dotnet build -c Release
dotnet publish -c Release -o publish
```

`publish\Mandal.exe` tek dosyadır; kurulu .NET 8 Desktop çalışma zamanını kullanır.
