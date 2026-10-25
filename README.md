# Profit-Aware Order Fulfilment Agent (Unity 6.3)

BLM5026 Bilgisayar Oyunlarında Yapay Zeka · Dönem projesi (Proje 2) · Veysel Uğurlu
Bursa Teknik Üniversitesi, Bilgisayar Mühendisliği Yüksek Lisans · Doç. Dr. İzzet Fatih Şentürk

Aynı ürün birden fazla depoda bulunabiliyor. Her sipariş geldiğinde ajan, siparişi hangi depodan göndereceğine ya da bölüp bölmeyeceğine karar veriyor. Bunu yaparken kargo maliyetini, gecikmeyi, bölünmüş gönderiyi ve stok tükenmesini dengeliyor. Simülasyon gerçek zamanlı bir Unity sahnesinde çalışıyor; aynı kod deneylerde de kullanılıyor.

- **Baseline:** en yakın depo, en düşük kargo maliyeti
- **Zorunlu AI 1:** açıklanabilir Utility AI (en az 4 normalize kriter, her kararın puan dökümü arayüzde)
- **Zorunlu AI 2:** zamanla karar ağırlıklarını uyarlayan öğrenen ajan (Contextual Bandit veya RL; vize sonrası)

## Durum

| Sprint | Tarih | Konu | Durum |
|---|---|---|---|
| S1 | 25–27 Eki | Kurulum, domain modeli, sipariş üretici, veri profili | tamamlandı |
| S2 | 28–30 Eki | Simülasyon motoru, stok, maliyet modeli, baseline'lar |  |
| S3 | 31 Eki–2 Kas | Harita, A* yol ağı, canlı görselleştirme | — |
| S4 | 3–5 Kas | Utility AI ve arayüz panelleri | — |
| S5 | 6–8 Kas | Deney altyapısı, ön sonuçlar, Paper Draft I | — |

Her sprint ayrı bir `feature/sNN-...` dalında geliştirilir ve Pull Request ile `main` dalına birleştirilir.

## Gereksinimler

- Unity 6000.3.x (6.3 LTS), Universal 2D, Input System
- Python 3.11 (veri hazırlama ve grafikler): `pip install -r tools/requirements.txt`

## Kurulum ve çalıştırma

1. Unity Hub → Add project from disk → bu klasör.
2. Ana sahne ve build S3'te eklenecek.

## Testler

EditMode testleri simülasyonun deterministik olduğunu (aynı seed → aynı sipariş akışı) ve ayarların föydeki minimumları karşıladığını kontrol eder.

```bash
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results/tests.xml
```

Unity Editor'da: Window → General → Test Runner → EditMode → Run All.

## Ayarlar

Bütün parametreler `Assets/StreamingAssets/configs/` altında JSON dosyalarıdır; kod değiştirmeden ayarlanır.

| Dosya | İçerik |
|---|---|
| `world.json` | 64×40 kurgusal şehir, 4 depo, 12 müşteri bölgesi, 30 ürün |
| `demand_profile.json` | saatlik talep, sepet büyüklüğü, adet, ürün popülerliği (veri setinden) |
| `scenarios/low.json`, `medium.json`, `high.json` | yük seviyeleri: 3 günde yaklaşık 630 / 1020 / 1620 sipariş, teslim sözü, stok günü, yenileme |
| `cost.json` | kargo, split cezası, gecikme, kayıp satış ve fırsat maliyeti katsayıları; hız ve işlem süresi |
| `regimes/stable.json`, `seasonal.json`, `volatile.json` | talep rejimleri: sabit, mevsimsel dalga, rastgele talep şokları |

## Veri seti

Talep deseni **UCI Online Retail II** veri setinden çıkarılır (Chen, 2012, [doi:10.24432/C5CG6D](https://doi.org/10.24432/C5CG6D), CC BY 4.0). Veriden yalnızca sipariş deseni alınır: saatlik dağılım, 1–5 ürünlük siparişlerin sepet büyüklüğü, satır başına adet ve en çok satan 30 ürünün sıralama-sıklık eğrisi. Depo ağı ve ürün kataloğu kurgusaldır. Ham veri repoya eklenmez; script onu indirilen zip dosyasından okur.

```bash
mkdir -p data/raw
curl -L -o data/raw/online_retail_ii.zip "https://archive.ics.uci.edu/static/public/502/online+retail+ii.zip"
python tools/prepare_retail_data.py data/raw/online_retail_ii.zip Assets/StreamingAssets/configs/demand_profile.json docs/data/retail_profile_stats.json
```

Temizleme adımları ve özet istatistikler `docs/data/retail_profile_stats.json` dosyasındadır.

## Hızlı kontrol

Unity menüsü **Fulfilment → Run Headless Check**: iki baseline × 3 yük × 3 rejim, tek seed. KPI tablosu Console'a, CSV'ler `results/_scratch/` altına yazılır.

```bash
Unity -batchmode -projectPath . -executeMethod Fulfilment.Editor.HeadlessRun.RunBatch -seed 1 -logFile log.txt
```

## Deneyleri tekrar üretme

S5'te eklenecek. Tüm politikalar aynı test seed'lerinde (1001–1010) koşturulacak, sonuçlar `results/` altına CSV/JSON olarak yazılacak ve makaledeki tablo ile grafikler `tools/` altındaki scriptlerle bu dosyalardan üretilecek.

## Proje yapısı

```
Assets/_Game/Scripts/Simulation/    saf C#: dünya, stok, sipariş üretici, rastgele sayı akışları (UnityEngine yok)
Assets/_Game/Scripts/AI/            saf C#: politikalar (baseline, Utility AI, öğrenen ajan)
Assets/_Game/Scripts/Experiments/   saf C#: deney koşucu
Assets/_Game/Scripts/Presentation/  Unity: sahne, arayüz, ayar yükleyici
Assets/_Game/Tests/EditMode/        birim testleri
Assets/StreamingAssets/configs/     JSON ayarlar
tools/                              veri hazırlama ve analiz scriptleri (Python)
docs/                               veri istatistikleri, görseller
```

AI, simülasyon ve arayüz katmanları ayrı assembly'lerdir. Simülasyon ve AI katmanı UnityEngine'e bağlı değildir; böylece canlı demo ile deney koşusu aynı kodu kullanır.

## Game AI Connection

S10'da tamamlanacak. Bu bölümde Utility AI'ın oyunlardaki rolü (NPC'lerin ihtiyaçlarına göre eylem seçmesi) ile bu projedeki rolü (siparişin karşılanma planını seçmek) karşılaştırılacak. Öğrenen ajanın oyunlardaki uyarlanabilir yapay zekâ ile bağlantısı da burada anlatılacak.

## Lisanslar

| Kaynak | Lisans |
|---|---|
| UCI Online Retail II veri seti | CC BY 4.0 |
| Kod | ders projesi, tüm hakları saklıdır |

Kullanılacak görsel asset'ler eklendikçe bu tabloya yazılacak.

## Yapay zekâ kullanımı

Ders politikası gereği belirtilir: çalışmanın hazırlanmasında yapay zekâ destekli araçlardan yararlanılmıştır. Kod çalıştırılarak ve testlerle kontrol edilmiştir.
