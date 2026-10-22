# Marques de discours

Les marques de discours sont de petites notes placées dans le texte d'une intervention, qui indiquent comment la prononcer : où faire une pause, ce qu'il faut dire lentement, ce qu'il faut accentuer, combien de temps chaque partie peut durer. Ce sont de simples textes, que vous pouvez saisir vous-même ou ajouter depuis le volet Discours, et vous pouvez montrer vous-même le texte annoté à un assistant IA : Caret n'envoie jamais rien.

## L'activer

**Affichage > Mode discours** affiche les marques sous forme de petites pastilles et ouvre le volet Discours (également sous **Marques de discours** dans la barre latérale). Le volet liste toutes les marques, la durée de chaque section et la liste des repères.

## Les marques

| Marque | Signification |
|---|---|
| `{pause 3s}` | une pause de trois secondes (`{beat}` dure une demi-seconde) |
| `{wait 5s: applaudissements}` | temps laissé au public |
| `{slow}...{/slow}`, `{fast}...{/fast}` | plus lentement ou plus vite |
| `{loud}...{/loud}`, `{soft}...{/soft}` | plus fort ou plus doucement |
| `{emphasis}...{/emphasis}` | à accentuer |
| `{tone: humour pince-sans-rire}...{/tone}` | n'importe quel ton, avec vos propres mots |
| `{cue: regarder le dernier rang}` | quelque chose à faire ou à ne pas oublier |
| `{wpm 130}` | vitesse de parole à partir de ce point, en mots par minute |
| `{budget 3m}` | temps accordé à cette section, placé après son titre |

Les mots-clés sont toujours en anglais, quelle que soit la langue du discours, afin qu'un fichier ait le même sens pour tout le monde. Une marque mal écrite reste du texte brut ; le volet les compte.

## Ajouter des marques

- Un *clic droit* en mode discours ouvre le *cercle des marques de discours* avec les marques à choisir. Le texte sélectionné est encadré par une paire de marques.
- `Ctrl+Maj+.` ajoute une pause, `Ctrl+Maj+,` un temps court et `Ctrl+Maj+E` une accentuation.
- **Paramètres > Marques de discours** vous permet de créer vos propres marques et recettes (plusieurs marques en un seul clic).

## Durée, téléprompteur, répétition

Le volet additionne le temps (les mots à votre rythme, plus les pauses) et le compare au budget de chaque section. **Affichage > Téléprompteur** ouvre une fenêtre qui fait défiler le texte à votre rythme, et une répétition enregistre le temps réellement mis par chaque partie. **Copier pour une IA** copie le discours avec une courte explication des marques. Le téléprompteur a sa propre rubrique dans le menu Aide.

## Points de départ

**Bibliothèque > Modèles > Ajouter les modèles de départ** comprend un discours (un toast) et une présentation (un exposé ou un pitch) déjà annotés, dans toutes les langues de Caret.