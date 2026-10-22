# Exporter vers Word

**Fichier** > **Exporter** > **Document Word (.docx)** écrit le document sous la forme d’un vrai fichier Word. Il est fait sur votre PC : Word n’a pas besoin d’être installé, et rien n’est envoyé.

## Ce que devient chaque élément

- Les titres sont des titres Word : le volet de navigation, une table des matières et un lecteur d’écran les retrouvent. Les listes, les tableaux (la ligne d’en-tête se répète sur chaque page), les citations, le code, les liens, le gras, l’italique et le reste sont conservés.
- Les images sont placées dans le fichier, à la taille qu’elles ont à l’écran, avec leur texte alternatif. Elles sont lues sur votre PC ; une image du web n’est pas téléchargée, et une image qui n’a pas pu être ajoutée est signalée dans un message, sa description restant dans le texte.
- Les notes de bas de page sont des notes de bas de page Word. Les formules (`$x^2$`) sont des équations Word. Les diagrammes (mermaid, flowchart, sequence, vega-lite) sont des images ; PlantUML a besoin d’un serveur, il reste donc du code.
- L’en-tête (« front matter ») au début du fichier donne le titre, l’auteur, le sujet, la description et les mots-clés du fichier Word, et n’est pas imprimé.
- Une ligne `[TOC]` devient une table des matières. Un lien vers un titre, comme `[voir plus bas](#budget)`, mène à ce titre. Word met à jour les numéros de page à l’ouverture du fichier.

## Révision et discours

Les commentaires et modifications écrits sous forme de marques (voir **Révision : commentaires et modifications**) deviennent de vrais commentaires et de vraies modifications suivies de Word, avec leurs auteurs et leurs jours : un collègue peut les accepter ou les refuser dans Word.

Quand vous suivez les modifications du document, Caret demande s’il faut les exporter : **Avec les modifications suivies** montre chaque modification depuis le début du suivi, et **Texte actuel seulement** exporte le texte tel qu’il est. Rien n’est écrit dans votre document dans les deux cas.

Les marques de discours (voir **Marques de discours**) sont de petites notes grises dans le fichier.

## Options

Les options s’ouvrent d’abord et sont conservées pour la fois suivante.

- **Apparence** : Simple, Rapport, Courrier ou Moderne. Elles changent les polices, les tailles, les couleurs et les espacements ; la structure est la même.
- **Taille de la page**, **Orientation** et **Marges**.
- **Texte de l’en-tête** et **Texte du pied de page** : `{title}` est remplacé par le titre du document et `{date}` par la date du jour. Les **Numéros de page** sont à droite du pied de page.
- **Table des matières au début**.
- **Modèle** : choisissez un fichier ou un modèle Word (`.docx`, `.dotx`) à vous avec **Choisir un modèle...**, et l’export est écrit dessus. Ses styles, sa page, son en-tête et son pied de page sont utilisés, et son propre texte est supprimé ; les options d’apparence et de page ne s’appliquent pas. **Retirer** revient aux apparences.
