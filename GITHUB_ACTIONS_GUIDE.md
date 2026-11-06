# 🍳 دليل بناء وتشغيل Aura Kitchen على GitHub Actions

أهلاً بك في **Aura Kitchen**! هذا المشروع مهيأ بالكامل ليتم بناؤه وتصديره سحابياً عبر **GitHub Actions** لمنصة الويب (**WebGL / Website**) ولمنصة الأندرويد (**Android APK**) دون الحاجة لتشغيل محرك Unity الثقيل على جهازك.

---

## 🚀 1. الميزات المتوفرة في المشروع

- **نظام زبائن متكامل (Customer AI & Dining System):**
  - وصول الزبائن إلى طاولات الطعام وطلب الوجبات.
  - عداد صبر ديناميكي (Patience Bar) يغير الألوان من الأخضر إلى الأحمر.
  - تفاعل الأكل وحركات الفرح وحساب الإكراميات (Tips).
  - مغادرة الزبائن الغاضبين عند انتهاء الوقت.
- **لعب فردي وجماعي (1 إلى 4 لاعبين):**
  - **Solo Shift (أوفلاين):** التبديل الفوري بين طباخين بالزر `Tab` أو زر الشاشة، مع مساعد ذكي (Sous-Chef Assistant).
  - **4-Player Co-op (أونلاين وعبر الشبكة):** 4 طهاة بألوان وهالات مميزة (Ruby Red, Sapphire Blue, Emerald Green, Amber Gold).
- **تصميم بيئة عمل واسع لـ 4 لاعبين:**
  - 3 محطات تقطيع، 3 أفران قلايات، محطات أطباق مزدوجة، صناديق خضار ولحم وخبز وجبن، طاولات طعام زبائن، وإضاءة دافئة مع لافتة نيون فاخرة.
- **واجهة مستخدم عصرية (Aura UI/UX):**
  - عداد إكراميات متحرك، شريط مضاعفة الحماس (Combo Streak `x4 MAX AURA 🔥`)، تقرير نهاية الوردية ونجوم ميشلان (1 إلى 5 نجوم).
- **أزرار تحكم باللمس (Mobile Touch Controls):**
  - عصا تحكم افتراضية (Virtual Joystick) وأزرار تفاعل وتقطيع جاهزة فوراً للهواتف وشاشات اللمس.

---

## 🌐 2. تجربة فورية بدون انتظار (Instant Playable Web)

قبل حتى رفع المشروع إلى GitHub، يمكنك تجربة اللعبة والتحكم فوراً:
1. قم بفتح ملف `index.html` أو `web/index.html` مباشرة بالنقر المزدوج في أي متصفح (Chrome, Edge, Firefox, أو على الهاتف).
2. تعمل اللعبة بتقنية 3D WebGL بـ 60 إطار في الثانية، وبأصوات تفاعلية ديناميكية وبدون أي استهلاك لموارد جهازك!

---

## ☁️ 3. كيفية البناء التلقائي عبر GitHub Actions

المشروع يحتوي على ملفات Workflow في المجلد `.github/workflows/`:
1. `deploy-webgl.yml`: يبني اللعبة كـ WebGL ويرفعها فوراً على **GitHub Pages** (موقع رسمي يمكنك لعبه من أي جهاز).
2. `build-android-apk.yml`: يبني ملف **APK** لنظام الأندرويد ويرفعه كـ Artifact و Release للتحميل المباشر.
3. `request-activation-file.yml`: استخراج ملف تفعيل ترخيص Unity الشخصي المجاني سحابياً بنقرة واحدة.

### خطوات الرفع والتشغيل:

#### أ) إنشاء مستودع على GitHub:
```powershell
git add .
git commit -m "Feat: Complete Aura Kitchen with 4-player co-op, customers, and CI/CD"
git branch -M main
git remote add origin https://github.com/USERNAME/aura-kitchen.git
git push -u origin main
```

#### ب) إضافة أسرار Unity في GitHub (Settings -> Secrets and variables -> Actions):
- `UNITY_EMAIL`: بريدك الإلكتروني في حساب Unity.
- `UNITY_PASSWORD`: كلمة مرور حساب Unity.
- `UNITY_LICENSE`: محتوى ملف الترخيص المجاني (يمكن الحصول عليه عبر تشغيل workflow `request-activation-file.yml` ثم تفعيله مجاناً في `license.unity3d.com`).

#### ج) تشغيل البناء:
بمجرد عمل `push`، ستقوم خوادم GitHub المجانية ببناء اللعبة وتوفير:
- رابط موقع مباشر عبر GitHub Pages.
- ملف `Aura-Kitchen.apk` جاهز للتثبيت على هواتف الأندرويد.

