# Déployer Caret dans une organisation

Guide destiné à la personne qui s'occupe des PC : le service informatique d'une grande entreprise, le référent d'une PME en croissance ou l'entrepreneur indépendant qui configure son propre portable. Il explique ce qu'est Caret, ce qu'il fait sur le poste et sur le réseau, et comment le déployer avec Microsoft Intune ou un autre outil. *(English version: [deployment.md](deployment.md).)*

## Quelle voie choisir

| Vous êtes | Voie conseillée |
| --- | --- |
| **Entrepreneur indépendant ou petite équipe sans postes gérés** | Installez Caret depuis le [Microsoft Store](https://apps.microsoft.com/detail/9n617shlqm8g) ou avec `winget` ([option A](#option-a--application-du-microsoft-store-recommandée)) : aucun droit d'administrateur requis. Seulement si vous ne pouvez pas utiliser le Store, prenez le package sur [GitHub Releases](https://github.com/fegyenc/Caret/releases) : il est signé avec le certificat du projet, et l'approuver une fois (`Caret.cer`, comme l'expliquent les notes de version) demande des droits d'administrateur ; ensuite, installations et mises à jour n'en demandent plus. |
| **PME avec Microsoft 365 Business Premium ou Intune** | Proposez Caret dans le Portail d'entreprise comme application Microsoft Store ([option A](#option-a--application-du-microsoft-store-recommandée)) ; définissez si vous le souhaitez les [valeurs par défaut](#définir-les-valeurs-par-défaut) pour que tout le monde démarre avec la même apparence. |
| **Grande entreprise** | Application Store via Intune, ou votre propre package signé ([option B](#option-b--msix-métier-line-of-business)) si chaque version doit être validée. Utilisez les [stratégies](#stratégies) pour désactiver la recherche de mises à jour et définir les valeurs par défaut. |

## En bref

| | |
| --- | --- |
| **Quoi** | Un éditeur Markdown à onglets avec un convertisseur intégré de Word, Excel, PowerPoint, PDF, CSV et des e-mails (Outlook `.msg`, `.eml`) vers Markdown, à coller dans les assistants d'IA avec beaucoup moins de jetons |
| **Éditeur / source** | Open source, licence MIT : <https://github.com/fegyenc/Caret> |
| **Package** | MSIX, par utilisateur. Aucun droit d'administrateur requis ; n'installe ni service, ni pilote, ni tâche planifiée. |
| **Architectures** | x64 et ARM64 |
| **Prérequis** | Windows 10 version 1809 ou ultérieure ; Windows 11 recommandé. Le runtime Microsoft Edge WebView2, inclus dans Windows 11 et installé avec Microsoft 365 Apps sur Windows 10. .NET et le Windows App SDK sont inclus dans le package. |
| **Langues** | Anglais, français, espagnol, polonais (suit la langue d'affichage de Windows ; modifiable par l'utilisateur) |
| **Compte / connexion** | Aucun |
| **Télémétrie** | Aucune |

## Données et réseau

- **Les documents ne quittent jamais le poste.** L'édition, l'enregistrement et la conversion se font localement. Aucun service cloud n'est associé à Caret.
- **Aucune IA intégrée.** Caret n'appelle aucun service d'IA ni modèle de langage et ne nécessite aucune clé d'API. Le masquage des données personnelles dans les e-mails (noms, adresses e-mail, numéros de téléphone, IBAN et numéros d'identité remplacés par des repères comme `[PERSON-1]`) se fait sur le poste, avec des règles fixes. Les noms sont repérés d'après les expéditeurs et destinataires de l'e-mail et dans les formules d'appel et de politesse : une personne seulement citée au milieu d'une phrase garde son nom. L'utilisateur vérifie le résultat et décide de ce qu'il colle dans un assistant.
- **Données de l'application** : paramètres (y compris la liste des documents ouverts en onglets, pour qu'ils se rouvrent), favoris, fichiers récents, modèles et sauvegardes de récupération sont stockés dans le dossier de données du package (`%LOCALAPPDATA%\Packages\<nom de famille du package>\LocalState`). Ils sont supprimés avec l'application.
- **Connexions sortantes possibles :**

| Destination | Quand | Version Microsoft Store | Version GitHub ou paquet déployé par vos soins | Désactivable |
| --- | --- | --- | --- | --- |
| `api.github.com` | Recherche de mises à jour : après une recherche réussie, aucune pendant 20 heures ; un échec est retenté au démarrage suivant | Jamais | Oui | Stratégie `DisableUpdateCheck` (ci-dessous), ou par l'utilisateur dans les Paramètres |
| `pypi.org`, `files.pythonhosted.org` | Installation du convertisseur facultatif MarkItDown, uniquement si l'utilisateur clique sur *Installer* | Jamais | Sur demande | Stratégie `DisableMarkItDownInstall` |
| Sites web cités dans une note | Images web affichées dans une note (comme un navigateur) | Oui | Oui | Non (dépend du contenu) |

La conversion Word, Excel, PowerPoint, PDF, CSV et des e-mails est intégrée et ne nécessite ni réseau, ni Outlook, ni Python. MarkItDown n'ajoute que des formats plus rares.

La politique de confidentialité se trouve dans [PRIVACY.md](../PRIVACY.md#français).

## Option A : application du Microsoft Store (recommandée)

Caret est dans le [Microsoft Store](https://apps.microsoft.com/detail/9n617shlqm8g) (ID Store `9N617SHLQM8G`). Intune le déploie et le met à jour directement depuis le Store.

1. Centre d'administration Intune → **Applications** → **Windows** → **Ajouter** → type d'application **Application Microsoft Store (nouveau)**.
2. **Rechercher dans l'application Microsoft Store (nouveau)** → *Caret* → sélectionnez-la. L'ID Store (commençant par `9`) est renseigné.
3. **Comportement d'installation : Utilisateur.** Caret est une application par utilisateur.
4. Affectez-la à des groupes en **Disponible pour les appareils inscrits** (installation depuis le Portail d'entreprise) ou **Obligatoire**.

Intune maintient l'application à jour via le Store ; les utilisateurs n'ont pas besoin de compte Store. Le même ID fonctionne avec winget :

```
winget install --source msstore --id 9N617SHLQM8G
```

## Option B : MSIX métier (line-of-business)

Pour les organisations qui n'utilisent pas le Store ou veulent contrôler chaque version :

1. Téléchargez le `.msix` depuis [GitHub Releases](https://github.com/fegyenc/Caret/releases), ou compilez-le depuis les sources.
2. **Signez-le avec le certificat de signature de code de votre organisation.** Le `Publisher` du package doit correspondre au sujet du certificat : il faut donc recompiler avec le nom de votre éditeur. Compilez depuis les sources avec les options de signature décrites dans [PACKAGING.md](../PACKAGING.md), après avoir défini `Identity/Publisher` dans `Package.appxmanifest` selon le sujet de votre certificat.
3. Vérifiez que le certificat est approuvé sur les postes (c'est généralement déjà le cas pour une autorité de signature interne).
4. Intune → Applications → Windows → Ajouter → **Application métier** → chargez le `.msix` → affectez.

Les mises à jour se redéploient de la même façon. Activez la stratégie `DisableUpdateCheck` pour que les utilisateurs ne soient pas avertis des versions GitHub non validées par votre organisation.

## Stratégies

Caret lit des valeurs sous `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Caret` (ou le même chemin sous `HKEY_CURRENT_USER`). Définissez-les avec les préférences de stratégie de groupe, un script de correction Intune ou un profil de configuration.

### Désactiver des fonctions

Valeurs DWORD :

| Valeur | Effet |
| --- | --- |
| `DisableUpdateCheck` = `1` | Pas de recherche de mises à jour sur GitHub ; le réglage est masqué et les Paramètres indiquent que les mises à jour sont gérées par votre organisation |
| `DisableMarkItDownInstall` = `1` | Caret n'exécute jamais `pip` ; pour les rares formats qui nécessitent MarkItDown, il explique comment l'installer |
| `DisableExplorerMenu` = `1` | Pas de **Convertir en Markdown** dans le menu contextuel de l'Explorateur de fichiers ; l'interrupteur de la page Convertir est désactivé et ne peut pas être réactivé |

Dans une installation Microsoft Store, les deux premiers comportements sont déjà actifs et ne peuvent pas être désactivés. `DisableExplorerMenu` fonctionne dans toutes les installations ; l'entrée de menu est ajoutée par le package, c'est donc le seul moyen de la retirer de façon centralisée.

Exemple (en tant qu'administrateur) :

```
reg add HKLM\SOFTWARE\Policies\Caret /v DisableUpdateCheck /t REG_DWORD /d 1 /f
reg add HKLM\SOFTWARE\Policies\Caret /v DisableMarkItDownInstall /t REG_DWORD /d 1 /f
reg add HKLM\SOFTWARE\Policies\Caret /v DisableExplorerMenu /t REG_DWORD /d 1 /f
```

### Définir les valeurs par défaut

Des valeurs de type chaîne (`REG_SZ`) qui fixent la configuration de départ, pour que toute une équipe ait la même apparence dès le premier jour. Ce sont des valeurs par défaut, pas des verrous : chacun peut encore les changer dans les Paramètres, et son choix est conservé. Les majuscules et minuscules sont indifférentes ; une valeur inconnue est ignorée. Si les deux ruches ont une valeur valide, `HKEY_LOCAL_MACHINE` passe en premier ; une valeur inconnue sous `HKEY_LOCAL_MACHINE` n'empêche pas une valeur valide sous `HKEY_CURRENT_USER` de s'appliquer.

| Valeur | Choix | Par défaut |
| --- | --- | --- |
| `DefaultLayout` | `classic` (Classique), `streamlined` (Épurée : menu et barre de mise en forme sur une seule ligne) | `streamlined` pour une nouvelle installation, `classic` pour une mise à jour depuis une version antérieure |
| `DefaultColorScheme` | `copper` (Cuivre), `paper` (Papier), `sage` (Sauge), `harbour` ou `harbor` (Port), `graphite` (Graphite) | `copper` |
| `DefaultAccentColor` | `scheme` (l'accent du jeu de couleurs), `windows` (la couleur d'accentuation choisie dans Windows) | `scheme` |
| `DefaultTheme` | `system`, `light` (clair), `dark` (sombre) | `system` |

Exemple : la disposition Épurée avec le jeu de couleurs Port et la couleur d'accentuation de Windows (en tant qu'administrateur) :

```
reg add HKLM\SOFTWARE\Policies\Caret /v DefaultLayout /t REG_SZ /d streamlined /f
reg add HKLM\SOFTWARE\Policies\Caret /v DefaultColorScheme /t REG_SZ /d harbour /f
reg add HKLM\SOFTWARE\Policies\Caret /v DefaultAccentColor /t REG_SZ /d windows /f
```

Les stratégies sont lues au démarrage de Caret. Une valeur par défaut ne s'applique à un réglage que tant que le fichier de paramètres de l'utilisateur n'en contient aucune valeur enregistrée : définissez-les donc avant le premier démarrage de Caret. Une nouvelle installation sans stratégie de disposition enregistre tout de suite Épurée, et modifier un réglage d'apparence dans les Paramètres enregistre ensemble le jeu de couleurs, l'accentuation et la matière de la fenêtre. Une valeur par défaut définie plus tard ne change pas ce qui est déjà enregistré.

## Sécurité

- **Capacités :** uniquement `runFullTrust`, la capacité standard des applications de bureau empaquetées (nécessaire pour ouvrir et enregistrer des fichiers à l'emplacement choisi par l'utilisateur). Comme tout programme de bureau, il peut utiliser le réseau ; les seules connexions qu'il établit sont listées ci-dessus.
- **Associations de fichiers :** Caret s'enregistre comme choix *Ouvrir avec* pour `.md`, `.markdown` et extensions similaires. Il ne devient pas l'application par défaut.
- **Liens dans les notes :** les liens web s'ouvrent dans le navigateur par défaut. Les liens vers des fichiers locaux n'ouvrent que des documents et des médias ; les exécutables et scripts sont affichés dans l'Explorateur de fichiers et jamais exécutés.
- **Composants tiers** et licences : [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). Les convertisseurs de documents sont l'Open XML SDK de Microsoft (MIT) et PdfPig (Apache 2.0).

## Désinstaller Caret

Désinstallez depuis Paramètres → Applications, ou retirez l'affectation Intune (Désinstaller). Les données de l'application sont supprimées avec le package ; les notes de l'utilisateur et les fichiers `.md` convertis restent là où ils ont été enregistrés.
