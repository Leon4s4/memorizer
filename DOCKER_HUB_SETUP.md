# Docker Hub Setup Instructions

This document explains how to configure Docker Hub publishing for the GitHub Actions workflow.

## Prerequisites

1. A Docker Hub account (create one at [hub.docker.com](https://hub.docker.com) if needed)
2. Admin access to this GitHub repository

## Step 1: Create Docker Hub Access Token

1. Log in to [Docker Hub](https://hub.docker.com)
2. Click on your username (top right) → **Account Settings**
3. Go to **Security** → **Access Tokens**
4. Click **New Access Token**
5. Give it a name (e.g., "GitHub Actions - Memorizer")
6. Set permissions: **Read, Write, Delete** (or **Read & Write** minimum)
7. Click **Generate**
8. **Copy the token immediately** (you won't be able to see it again!)

## Step 2: Add Secrets to GitHub Repository

1. Go to your GitHub repository: `https://github.com/Leon4s4/memorizer`
2. Click **Settings** (top menu)
3. In the left sidebar, click **Secrets and variables** → **Actions**
4. Click **New repository secret**

### Add First Secret: DOCKERHUB_USERNAME
- **Name**: `DOCKERHUB_USERNAME`
- **Secret**: Your Docker Hub username (e.g., `leon4s4`)
- Click **Add secret**

### Add Second Secret: DOCKERHUB_TOKEN
- Click **New repository secret** again
- **Name**: `DOCKERHUB_TOKEN`
- **Secret**: Paste the access token you copied in Step 1
- Click **Add secret**

## Step 3: Verify Setup

1. Go to **Actions** tab in your GitHub repository
2. The next push to `main` branch will trigger the workflow
3. The workflow will now push images to **both**:
   - GitHub Container Registry: `ghcr.io/leon4s4/memorizer`
   - Docker Hub: `leon4s4/memorizer`

## Docker Hub Repository

After the first successful push, your image will be available at:
- **Public URL**: https://hub.docker.com/r/leon4s4/memorizer
- **Pull command**: `docker pull leon4s4/memorizer:latest`

### Making the Repository Public

By default, Docker Hub repositories are public. If you want to ensure it's public:

1. Go to [hub.docker.com](https://hub.docker.com)
2. Click **Repositories**
3. Find `memorizer`
4. Click **Settings**
5. Under **Visibility**, ensure it's set to **Public**

## Using the Published Images

### From Docker Hub (Recommended for Public Use)
```bash
docker pull leon4s4/memorizer:latest
docker run -d -p 9000:8000 -v memorizer-data:/app/data leon4s4/memorizer:latest
```

### From GitHub Container Registry
```bash
docker pull ghcr.io/leon4s4/memorizer:latest
docker run -d -p 9000:8000 -v memorizer-data:/app/data ghcr.io/leon4s4/memorizer:latest
```

## Available Tags

The workflow automatically creates these tags:
- `latest` - Latest build from main branch
- `main` - Latest build from main branch
- `dev` - Latest build from dev branch
- `main-<sha>` - Specific commit SHA
- `v1.0.0`, `v1.0`, `v1` - For tagged releases (if you create GitHub releases)

## Troubleshooting

### "Invalid username or password"
- Make sure you're using an **Access Token**, not your Docker Hub password
- Verify the token has **Read & Write** permissions
- The token might have expired - create a new one

### "Repository not found"
- The repository will be automatically created on first push
- Ensure your Docker Hub username in the secret matches your actual username

### "Quota exceeded"
- Free Docker Hub accounts have pull rate limits
- Consider upgrading to Docker Hub Pro if needed
- GitHub Container Registry has no pull limits for public packages

## Security Notes

- ✅ Access tokens are more secure than passwords
- ✅ Tokens can be revoked without changing your password
- ✅ Use separate tokens for different workflows/projects
- ⚠️ Never commit tokens or credentials to git
- ⚠️ GitHub Secrets are encrypted and only exposed during workflow execution

## Additional Resources

- [Docker Hub Access Tokens Documentation](https://docs.docker.com/docker-hub/access-tokens/)
- [GitHub Actions Secrets Documentation](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
- [Docker Build Push Action](https://github.com/docker/build-push-action)
