# Convertir des documents et des e-mails

Caret transforme des documents en Markdown : un texte propre qui conserve les titres, les listes, les tableaux, les liens et les notes de bas de page, et laisse de côté les polices et la mise en page. Le résultat ne représente généralement qu'une petite partie de la taille d'origine, il est facile à lire pour les personnes et bien plus léger en jetons pour les assistants IA. Tout est converti sur votre PC : rien n'est envoyé en ligne.

## Documents

Ouvrez **Convertir en Markdown** dans la barre latérale et déposez des fichiers ou un dossier entier, ou utilisez **Choisir des fichiers...**. Word, Excel, PowerPoint, PDF et CSV sont pris en charge. Chaque résultat indique sa taille avant et après, ainsi qu'une estimation de ses jetons d'IA. **Tout copier pour l'IA** place l'ensemble dans le presse-papiers sous forme d'un seul texte.

Vous pouvez aussi faire un clic droit sur un fichier, plusieurs fichiers ou un dossier dans l'*Explorateur de fichiers* et choisir **Convertir en Markdown** (indisponible lorsque votre organisation le désactive).

Les fichiers Markdown sont enregistrés à côté des originaux, ou dans un dossier de votre choix. Un fichier déjà présent n'est jamais écrasé.

## E-mails Outlook

Ouvrez **E-mails Outlook** et choisissez des e-mails enregistrés depuis Outlook (`.msg`) ou depuis d'autres programmes de messagerie (`.eml`). Caret transforme toute la conversation en un seul fichier : chaque réponse devient un message distinct, du plus ancien au plus récent, sans signatures, sans mentions légales ni bandeaux « expéditeur externe », et les pièces jointes sont converties à leur place.

**Masquer les données personnelles** (activé par défaut) remplace les noms, adresses e-mail, numéros de téléphone, numéros de compte bancaire et numéros d'identité par des repères tels que `[PERSON-1]`, le même repère pour la même personne, de sorte que la conversation reste lisible lorsque vous la collez dans un assistant IA. Cela fonctionne avec des règles fixes et des chiffres de contrôle, pas avec de l'IA. Les personnes sont repérées à partir des expéditeurs et des destinataires de l'e-mail ainsi que des formules d'appel et de politesse : un nom qui n'apparaît qu'au milieu d'une phrase n'est donc pas trouvé. Vérifiez le résultat avant de le partager.

Les e-mails en anglais, français, espagnol, polonais et portugais sont compris, et les règles sont de simples fichiers qu'une entreprise peut compléter.

## Déposer des fichiers n’importe où

Pas besoin d’ouvrir d’abord la page de conversion : faites glisser des fichiers Word, PDF, Excel, PowerPoint ou des courriels (ou tout un dossier) sur la fenêtre de Caret, n’importe où, et relâchez-les quand **Convertir en Markdown** apparaît. Un petit panneau en bas à droite les liste, chacun avec une barre de progression et, quand le pointeur le survole ou que le clavier s’y trouve, **Ouvrir**, **Afficher dans l'Explorateur de fichiers** (le bouton dossier) et **Comparer**. Un fichier en échec en donne la raison, avec **Réessayer** ; **Effacer la liste** vide le panneau et Échap le ferme.

**Comparer** affiche l’original en texte brut à gauche et le Markdown à droite, et les fait défiler ensemble. Au-dessus figurent les choix utilisés : si les images ont été extraites et, pour un courriel, si les données personnelles ont été masquées et combien de noms, d’adresses, de numéros et d’identifiants ont été trouvés (ils sont en gras et soulignés dans le Markdown). Échap le ferme. Servez-vous-en pour voir ce que la conversion a gardé, écarté ou masqué avant de partager le résultat.

## Quand quelque chose ne se convertit pas

Un fichier protégé par un mot de passe, ou endommagé, est listé avec la raison. Un PDF numérisé sans texte ne peut pas être lu : Caret ne fait pas de reconnaissance de texte.