# Microsoft Store listing

What goes into Partner Center for **Change Text Case** (product "TextCase by QuentinPerou", Store ID `9N6FJCHCW5XW`),
page by page, ready to paste. Kept here so that the next submissions start from it. The package: `dist\store\TextCase_<version>.0_x64.msix`
(`scripts\build-store.ps1`). The Store version has no menu in File Explorer (see `store\AppxManifest.xml`): these texts
do not mention it.

## Pricing and availability

- Markets: all. Visibility: public. Price: free (no trial, no add-ons).
- Publish: as soon as it passes certification.

## Properties

- Category: **Productivity**. Secondary category: **Utilities & tools**.
- Does the app access, collect or transmit personal information? **Yes**: it reads the text the user selects and
  watches the keyboard for its shortcuts, on the PC only (nothing kept, nothing sent). Hence the privacy policy below.
- Privacy policy URL: `https://github.com/quentinperou/TextCase#privacy`
- Website: `https://github.com/quentinperou/TextCase`
- Support contact info: `https://github.com/quentinperou/TextCase/issues`
- Product declarations: the defaults (install to alternate drives, OneDrive backups); nothing else.
- System requirements: keyboard.

## Age ratings (IARC questionnaire)

Category: utility / productivity / other. Every question **No**: no violence, no user interaction or shared content,
no location shared, no purchases, no unrestricted web access. Expected rating: 3+ (PEGI 3).

## Packages

Upload the `.msix`. Device family: Windows 10/11 Desktop (the package needs Windows 10 2004 or later).

## Submission options

**Why do you need the runFullTrust capability?** (500 characters at most)

```
Desktop app (WPF, .NET) that changes the case of the selected text in any app. Full trust is needed to: see its keyboard shortcuts in every app (a low-level keyboard hook that only looks for them and keeps nothing); send Ctrl+C and Ctrl+V to the app in front to copy the selection and paste the result; read and put back the clipboard; show its popup without taking the focus (Win32); start with Windows (startup task). No data leaves the PC.
```

**Notes for certification**

```
No account, no sign-in. Open Notepad, type a line and select it, then press Ctrl+Alt+C: a popup shows the result of each case; press 1 to 4 or click one, and the selection is replaced. Ctrl+C twice (hold Ctrl, press C twice) on a selection opens the same popup. Direct shortcuts: Ctrl+Alt+U (UPPERCASE), Ctrl+Alt+L, Ctrl+Alt+T, Ctrl+Alt+S. The settings open from the Start menu (TextCase) or the notification area icon. An app run as administrator cannot receive keys from TextCase.
```

## Store listing: English (en-us)

**Description**

```
Change the case of the selected text in any Windows app, even through Remote Desktop.

Select some text and press Ctrl+Alt+C: a popup shows the result of each case. Choose one with a key, the arrows or the mouse, and the selection is replaced, as plain text. Or press Ctrl+C twice to open it on what you have just copied. Direct shortcuts convert at once: Ctrl+Alt+U, L, T, S.

17 cases: UPPERCASE, lowercase, Title Case, Sentence case, camelCase, PascalCase, CONSTANT_CASE, param-case, snake_case, dot.case, tOGGLE cASE, aLtErNaTiNg, no accents, no case, and clean-ups (extra spaces, line breaks, plain text).

Smart Title Case and Sentence case: "CANON EOS R6 MARK II" becomes "Canon EOS R6 Mark II", with a list of words kept as written.

Light on your PC: it waits in the notification area and uses almost nothing. The text you convert never leaves your computer.

Free and open source (MIT).
```

**Product features** (one per field)

```
The result of each case in a popup: Ctrl+Alt+C on a selection
Ctrl+C twice: the popup on what you have just copied
Direct shortcuts: Ctrl+Alt+U, L, T, S, one for each case if you like
Pasted back as plain text, the clipboard put back after
Works in the Remote Desktop window, nothing to install on the remote PC
17 cases, from UPPERCASE to snake_case, and clean-ups
Smart Title Case: brands, acronyms and model names kept
Your own list of words kept as written
Light or dark theme, English and French
Starts with Windows, uses almost nothing at rest
```

**Short description**

```
Change the case of the selected text in any app, even through Remote Desktop: a shortcut, a popup with the result of each case, and the text pasted back as plain text.
```

**Screenshots**: `.github/screenshot.png` (1560 × 810).

**Search terms** (one per field)

```
change case
uppercase
lowercase
title case
sentence case
case converter
text case
```

**Copyright and trademark info**

```
Copyright (c) 2026 QuentinPerou.
```

**Developed by**: `QuentinPerou`. **What's new in this version**: empty for the first submission.

## Store listing: French (fr-fr)

**Description**

```
Changez la casse du texte sélectionné dans n'importe quelle application Windows, même par le Bureau à distance.

Sélectionnez du texte et faites Ctrl+Alt+C : une popup montre le résultat de chaque casse. Choisissez-en une avec une touche, les flèches ou la souris, et la sélection est remplacée, en texte brut. Ou faites Ctrl+C deux fois pour l'ouvrir sur ce que vous venez de copier. Des raccourcis directs convertissent tout de suite : Ctrl+Alt+U, L, T, S.

17 casses : MAJUSCULES, minuscules, Chaque Mot, Début de phrase, camelCase, PascalCase, CONSTANT_CASE, param-case, snake_case, dot.case, iNVERSER, aLtErNeR, sans accents, mots séparés, et le nettoyage (espaces en trop, sauts de ligne, texte brut).

Chaque Mot et Début de phrase intelligents : « CANON EOS R6 MARK II » devient « Canon EOS R6 Mark II », avec une liste de mots gardés tels quels.

Léger pour votre PC : il attend dans la zone de notification et ne consomme presque rien. Le texte que vous convertissez ne quitte jamais votre ordinateur.

Gratuit et open source (MIT).
```

**Fonctionnalités** (une par champ)

```
Le résultat de chaque casse dans une popup : Ctrl+Alt+C sur une sélection
Ctrl+C deux fois : la popup sur ce que vous venez de copier
Raccourcis directs : Ctrl+Alt+U, L, T, S, un par casse si vous voulez
Recollé en texte brut, le presse-papiers remis ensuite
Marche dans la fenêtre du Bureau à distance, rien à installer sur le PC distant
17 casses, des MAJUSCULES au snake_case, et le nettoyage
Chaque Mot intelligent : marques, sigles et noms de modèles gardés
Votre liste de mots gardés tels quels
Thème clair ou sombre, français et anglais
Démarre avec Windows, ne consomme presque rien au repos
```

**Description courte**

```
Changez la casse du texte sélectionné dans n'importe quelle application, même par le Bureau à distance : un raccourci, une popup avec le résultat de chaque casse, et le texte recollé en texte brut.
```

**Captures d'écran** : `.github/screenshot.fr.png` (1560 × 810).

**Termes de recherche** (un par champ)

```
changer la casse
majuscules
minuscules
casse
convertisseur de casse
première lettre majuscule
title case
```

**Copyright et marques**

```
Copyright (c) 2026 QuentinPerou.
```

**Développé par** : `QuentinPerou`. **Nouveautés de cette version** : vide pour la première soumission.
