# Make10 — Google Play Console Setup Guide

**App type:** Paid ($0.99) · No ads · No data collection

---

## Before You Start

You need:
- A Google Play Developer account ($25 one-time registration fee)
- A hosted privacy policy URL (use the included `privacy_policy.html`)
- Your signed AAB file (Android App Bundle) from Unity

### Hosting the Privacy Policy

Upload `privacy_policy.html` to any of these:
- **GitHub Pages** (free) — push to a repo, enable Pages, link is `https://yourname.github.io/repo/privacy_policy.html`
- **Your own website** — e.g. `https://wizardbodega.com/make10/privacy`
- **Google Sites** (free) — copy-paste the text content into a Google Site page

You'll paste this URL into Play Console in multiple places.

---

## Step-by-Step: Play Console Setup

### 1. Create the App

1. Go to [Google Play Console](https://play.google.com/console)
2. Click **Create app**
3. Fill in:
   - **App name:** Make10
   - **Default language:** English (United States)
   - **App or game:** Game
   - **Free or paid:** **Paid**
4. Accept the declarations and click **Create app**

### 2. Set the Price

1. Go to **Monetize → Products → App pricing**
2. Set default price: **$0.99 USD**
3. Click **Set prices by country** to auto-convert to other currencies, or customize per-country

> Note: You must have a merchant account set up in Play Console to sell paid apps. If you haven't, you'll be prompted to create one.

### 3. Store Listing

Go to **Grow → Store presence → Main store listing** and fill in:

| Field | Value |
|-------|-------|
| App name | Make10 |
| Short description | Swap tiles. Make 10. A number puzzle game with two modes — 60-second Arcade sprint and 5-minute MakeZen meditation. |
| Full description | (See suggested text below) |
| App icon | 512×512 PNG, no transparency |
| Feature graphic | 1024×500 PNG |
| Screenshots | Min 2, recommended 4-8 per device type (phone required) |

**Suggested full description:**

```
Make10 is a number puzzle game where you swap tiles to create rows and columns that sum to multiples of 10.

Two modes:
• Arcade — a 60-second sprint. Build your multiplier, trigger Hot Streaks, chase high scores.
• MakeZen — a 5-minute math meditation. Matched tiles lock in place, building board pressure until the grid fills or time runs out.

No ads. No data collection. Just puzzles.

Features:
• Simple to learn, deep to master
• Two distinct play modes
• Progressive difficulty that adapts as you improve
• Satisfying match feedback and visual effects
• Star ratings and personal high scores

Research shows 15-20 minutes of daily simple arithmetic practice improves focus and processing speed. Three 5-minute MakeZen sessions = the optimal dose.

Made by Wizard Bodega.
```

### 4. App Content (Declarations)

Go to **Policy → App content**. You need to complete ALL of these sections:

#### A. Privacy Policy
- Paste your hosted privacy policy URL

#### B. Ads Declaration
- **Does your app contain ads?** → **No**

#### C. App Access
- **Is all of your app's functionality available without any access restrictions?** → **Yes, all functionality is available without special access**
  - (Make10 has no login, no paywall gating beyond the purchase, no restricted content)

#### D. Content Rating (IARC Questionnaire)
Answer the IARC questionnaire. For Make10, the answers are straightforward:

| Question | Answer |
|----------|--------|
| Violence | No |
| Sexuality / nudity | No |
| Language / profanity | No |
| Controlled substances | No |
| Gambling (real money) | No |
| Simulated gambling | No |
| User-generated content | No |
| Users can interact/communicate | No |
| Shares user location | No |
| Allows digital purchases | No (it's a paid upfront app, no IAP) |

This should result in an **Everyone / PEGI 3 / USK 0** rating.

#### E. Target Audience and Content
- **Target age group:** Select all applicable. Since Make10 is a math game suitable for everyone, you can select **all age groups** including under 13.
  - ⚠️ If you select any age group under 13, Google will apply Families Policy requirements. Since you have no ads, no data collection, and no user communication, you should be compliant. But review the [Families Policy](https://support.google.com/googleplay/android-developer/answer/9893335) to confirm.
  - **Simpler option:** Select only **13 and above** to avoid Families Policy entirely. The game is still accessible to all ages since parents control purchases.
- **Does the app appeal to children?** → Answer based on your target audience selection above.

#### F. News App
- **Is this a news app?** → **No**

#### G. COVID-19 Contact Tracing / Health Apps
- If asked, → **No**

#### H. Data Safety Form
This is the big one. Here's exactly what to select:

**Overview:**
- **Does your app collect or share any of the required user data types?** → **No**

Since you answered No, you'll confirm:
- Your app does not collect user data
- Your app does not share user data with third parties
- All user data handled by your app is encrypted in transit → **N/A** (no data transmitted)

You'll also need to confirm:
- **Do you provide a way for users to request that their data is deleted?** → You can select **No** since no data is collected. Or note that local data is deleted when the app is uninstalled.
- **Security practices:** Check **"Data is not transferred to third parties"**

That's it for Data Safety. Since you have zero data collection, no third-party SDKs, and no ads, this is the simplest possible form.

#### I. Government Apps
- **Is this a government app?** → **No**

#### J. Financial Features
- **Does this app provide financial services?** → **No**

### 5. Pre-launch Report (Optional but Recommended)

Go to **Test → Pre-launch report**. Google automatically runs your app on Firebase Test Lab devices. Check for:
- Crashes
- Accessibility warnings
- Security vulnerabilities

### 6. Release

1. Go to **Release → Production**
2. Click **Create new release**
3. Upload your signed `.aab` file
4. Fill in release notes (e.g., "Initial release of Make10 — number puzzle game with Arcade and MakeZen modes")
5. Click **Review release**
6. Click **Start rollout to Production**

---

## Unity-Specific Notes

### The `com.unity.modules.unityanalytics` Module

Your `manifest.json` includes this module. It's a **built-in Unity module stub** — NOT the Unity Gaming Services (UGS) Analytics package. It does NOT collect or transmit data unless you:
1. Explicitly install `com.unity.services.analytics` from Package Manager
2. Enable Analytics in the Unity Dashboard
3. Call Analytics APIs in code

**You haven't done any of these**, so it's safe to declare "no data collection" in the Data Safety form.

If you want to be extra cautious, you can remove it from `Packages/manifest.json`:
```
"com.unity.modules.unityanalytics": "1.0.0"  ← remove this line
```
This won't affect your game.

### Other Unity Modules

The modules in your manifest (`com.unity.modules.*`) are core Unity engine modules (physics, audio, UI, etc.). None of them collect or transmit user data.

---

## Summary Checklist

- [ ] Host privacy policy HTML at a public URL
- [ ] Create app in Play Console as **Paid Game**
- [ ] Set price to $0.99
- [ ] Complete store listing (name, description, icon, screenshots, feature graphic)
- [ ] Complete Privacy Policy declaration (paste URL)
- [ ] Complete Ads declaration → No
- [ ] Complete App Access → All functionality available
- [ ] Complete Content Rating questionnaire → Expect "Everyone"
- [ ] Complete Target Audience → 13+ (simplest) or all ages
- [ ] Complete Data Safety form → No data collected, no data shared
- [ ] Upload signed AAB
- [ ] Review and publish
