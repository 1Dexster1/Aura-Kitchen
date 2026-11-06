# 📡 Aura Kitchen Real-time Multiplayer Backend

خادم ألعاب سريع وخفيف مبني باستخدام **Node.js** و **WebSockets** و **Express** لإدارة الغرف والـ 4 لاعبين والمزامنة اللحظية ومجلس الصدارة (Leaderboard) للعبة **Aura Kitchen**.

---

## ⚡ 1. التشغيل المحلي (Local Run)

الخادم مثبت بالكامل وجاهز للتشغيل بأمر واحد:
```powershell
cd server
npm start
```

بمجرد تشغيل الأمر:
- يعمل الخادم على الرابط: `http://localhost:3000`
- يستضيف خادم الـ WebSocket للملتي بلاير على نفس المنفذ: `ws://localhost:3000`
- يقدم ملفات اللعبة مباشرة، مما يتيح لك فتح `http://localhost:3000` في عدة متصفحات للعب معاً فوراً!

---

## ☁️ 2. النشر السحابي المجاني (1-Click Free Cloud Deploy)

يمكنك استضافة الباك إند مجاناً مدى الحياة على أي منصة سحابية:

### أ) النشر على Render.com (موصى به مجاناً):
1. قم بإنشاء حساب مجاني على [Render.com](https://render.com).
2. اختر **New Web Service** واربط المستودع الخاص بك على GitHub.
3. اضبط الإعدادات التالية:
   - **Root Directory**: `server`
   - **Build Command**: `npm install`
   - **Start Command**: `node server.js`
4. احصل على الرابط المجاني (مثلاً `https://aura-kitchen.onrender.com`) واستخدمه مباشرة في اللعبة!

### ب) النشر عبر Docker:
يحتوي مجلد `server/` على [Dockerfile](file:///d:/Aura%20Kitchen/server/Dockerfile) جاهز للبناء على أي خادم:
```bash
docker build -t aura-kitchen-server .
docker run -p 3000:3000 aura-kitchen-server
```

---

## 🔌 3. واجهات البرمجة (REST API)

| Endpoint | Method | الوصف |
| :--- | :--- | :--- |
| `/api/status` | `GET` | فحص حالة السيرفر وعدد الغرف واللاعبين المتصلين |
| `/api/leaderboard` | `GET` | قائمة أفضل فرق الطهاة والمبالغ والتقييمات |
| `/api/score` | `POST` | تسجيل نتيجة وردية جديدة في قائمة الشرف |

---

## 🎮 4. بروتوكول الـ WebSocket التفاعلي

- `create_room`: ينشئ غرفة جديدة ويرجع كود دخول من 4 أحرف (مثل `AURA`).
- `join_room`: الانضمام إلى غرفة موجودة وتوزيع تلقائي للون الشيف (Ruby, Sapphire, Emerald, Amber).
- `player_move`: مزامنة حركة ودوران الشيف بتردد 30Hz.
- `counter_interact`: مزامنة رفع ووضع الأطباق وتجهيز البرجر والسلطة.
- `chop_action`: مزامنة تقدم التقطيع بالسكين والصوت.
- `order_delivered`: احتساب الأرباح وإشعار جميع اللاعبين بالنجاح.

