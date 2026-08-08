# Publishing guide

1. Open a terminal in the project root.
2. Ensure the repository contains the files you want to publish, especially the project folder and the root files such as README.md, LICENSE, .gitignore, and .gitattributes.
3. Initialize Git if needed:

```powershell
git init
git add .
git commit -m "Initial commit"
```

4. Create a GitHub repository and push:

```powershell
git remote add origin https://github.com/<your-user>/<your-repo>.git
git branch -M main
git push -u origin main
```

## Privacy reminder
- Only publish the project folder and its source files.
- Do not include personal data, local app data, backups, logs, or system-specific artifacts.
- Keep the app focused on local, user-controlled optimization.
