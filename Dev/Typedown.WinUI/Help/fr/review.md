# Révision : commentaires et modifications

La fonction de révision fonctionne comme les outils de révision d'un traitement de texte, mais tout est écrit dans le document sous forme de texte brut, de sorte qu'un collègue, un autre éditeur et un assistant IA peuvent aussi le lire. Les marques suivent une convention publique appelée CriticMarkup :

| Ce que vous voyez | Écrit dans le fichier sous la forme |
|---|---|
| texte ajouté en vert | `{++ajouté++}` |
| texte supprimé en rouge | `{--supprimé--}` |
| un remplacement | `{~~ancien~>nouveau~~}` |
| un commentaire jaune | `{==le texte==}{>>@Nom 2026-10-07: la note<<}` |

Rien n'est conservé ailleurs, et la révision ne démarre jamais d'elle-même : saisir ou supprimer du texte ne crée pas de marques.

## Ajouter un commentaire

Sélectionnez du texte, faites un clic droit, **Ajouter un commentaire** (ou **Révision > Ajouter un commentaire**). Écrivez la note et vérifiez votre nom (au départ, c'est votre nom d'utilisateur Windows). Le texte est surligné en jaune, avec la note à côté. Elle est saisie dans le document, donc **Annuler** fonctionne.

## Comparer avec un autre fichier

**Révision > Comparer avec un autre fichier...** demande une version antérieure du document (par exemple la copie que vous avez envoyée) et le nom de l'auteur des modifications. Caret écrit les différences entre cette version et le document affiché à l'écran sous forme de révision *dans un nouvel onglet* ; votre document n'est pas modifié. Servez-vous-en pour voir ce qu'un collègue a changé dans le fichier qu'il vous a renvoyé. Si les onglets sont désactivés (**Paramètres > Onglets et fenêtres**), la révision remplace votre document dans la fenêtre : enregistrez donc votre document d'abord.

## Suivre les modifications pendant que vous éditez

**Révision > Suivre les modifications** (aussi un bouton dans le panneau Révision) retient le document tel qu’il est maintenant. Pendant que vous éditez, un instant après que vous avez cessé de taper, Caret montre ce que vous avez changé : le volet de droite de l’affichage **Fractionné** dessine le document avec les ajouts en vert et les suppressions en rouge, chacun avec votre nom et le jour, et le panneau Révision liste les modifications et les compte (aussi dans la barre d’état). Rien n’est écrit dans le fichier. **Arrêter le suivi** oublie la version de départ et laisse le document tel quel. La liste sert aussi à se déplacer dans le document : *cliquez sur une modification* et Caret y saute dans l’éditeur et la met en évidence dans le panneau de droite, quelle que soit la longueur du document. **Modification précédente** et **Modification suivante** passent de l’une à l’autre. La ✓ à côté d’une modification l’accepte (elle devient partie de la version de départ et quitte la liste) ; la ✗ la rejette (l’ancien texte revient dans le document, et **Annuler** fonctionne). **Annuler l’acceptation** reprend les dernières modifications acceptées. **Accepter toutes les modifications** et **Rejeter toutes les modifications** font de même pour toutes les modifications suivies d’un coup. Les différences dans les blocs de code ne peuvent être acceptées ou rejetées que toutes ensemble.

Dans l’affichage Visuel, les modifications sont dessinées dans le texte lui-même : le texte ajouté a un fond vert, et une petite marque rouge se tient là où du texte a été supprimé. Pointez une modification ou une marque : une carte montre l’ancien texte, qui et quand, avec **Accepter la modification** et **Rejeter la modification**. Un clic sur une modification dans le panneau Révision y fait défiler le document et l’entoure un instant. Une modification que Caret ne retrouve pas dans le texte de la page n’y est pas dessinée, mais reste dans la liste et dans l’affichage Fractionné.

Le suivi continue quand vous fermez le fichier puis le rouvrez : Caret garde la version de départ, votre nom et le jour où chaque modification a été vue pour la première fois dans son propre dossier de données (jamais dans votre fichier, et pas pour un document jamais enregistré), et les modifications sont listées de nouveau à l’ouverture du fichier. Une modification garde le jour où elle a été vue pour la première fois ; elle ne devient pas celle du jour chaque matin. Si le fichier a été modifié dans un autre programme entre-temps, ces modifications apparaissent aussi comme des modifications, et le panneau le dit. **Arrêter le suivi** supprime ce qui a été gardé, tout comme déplacer le fichier dans la Corbeille depuis Caret ; ce qui n’est pas ouvert pendant 90 jours est nettoyé.

**Écrire les modifications dans le document** transforme les modifications suivies en marques de révision dans le texte lui-même : chaque modification devient `{++ajouté++}`, `{--supprimé--}` ou `{~~avant~>après~~}` suivi de votre nom et du jour où elle a été vue pour la première fois, les mêmes marques que celles qu’écrit **Comparer avec un autre fichier...**. Elle demande d’abord, c’est une seule étape d’**Annuler** et le suivi s’arrête. Dès lors ce sont des marques ordinaires : envoyez le fichier à un collègue, et acceptez ou rejetez-les une par une (clic droit sur une modification) ou toutes à la fois (**Accepter toutes les modifications**, **Rejeter toutes les modifications**). Les différences qui n’ont pas pu être marquées restent du texte ordinaire, et la fenêtre le dit.

## Accepter et rejeter

- *Clic droit sur une modification* (verte, rouge ou un remplacement) : **Accepter la modification** conserve ce qu'elle indique (les ajouts restent, les suppressions disparaissent) ; **Rejeter la modification** remet l'ancien texte.
- *Clic droit sur un commentaire :* **Supprimer le commentaire**.
- **Révision > Accepter toutes les modifications**, **Rejeter toutes les modifications** et **Supprimer tous les commentaires** s'appliquent à tout le document, en une seule étape d'annulation.

## Masquer les marques

**Paramètres > Éditeur > Afficher les marques de révision** désactive les couleurs ; les marques apparaissent alors comme le texte brut qu'elles sont.

## Bon à savoir

- Les marques sont du texte : un fichier révisé peut donc être enregistré, envoyé et ouvert dans n'importe quel éditeur.
- Un fichier renvoyé avec les marques d'un collègue se lit de la même façon : ses modifications apparaissent en couleur et vous les acceptez ou les rejetez.