# Eat My Entire Ass 🍑

A deliciously irreverent web-based clicking game where you race against time to devour cartoon posterior delicacies. Built with .NET 9 Blazor WebAssembly and Azure Functions for the modern connoisseur of absurd browser entertainment.

## 🎮 What Is This?

"Eat My Entire Ass" is a satirical 60-second clicking game that challenges players to consume as many cartoon asses as possible before time runs out. Each ass type has different point values and visual/audio feedback, making for a surprisingly engaging (and ridiculous) gaming experience.

### Game Features

- **6 Unique Ass Types** with different point values:
  - 🦴 **Boney** (0.5 pts) - Weirdly crunchy, zero nutritional value
  - 🎨 **Cartoon** (1 pt) - Hand-drawn with MS Paint flavor  
  - 📏 **Flat** (1 pt) - Flatter than Home Depot lumber
  - 🌿 **Hairy** (1 pt) - Natural and unshaven, as intended
  - 🍑 **GYAT** (2 pts) - Thicc and juicy goodness
  - 🏆 **Golden** (10 pts) - The holy grail of posterior consumption

- **60-Second Timer** - Race against the clock!
- **Visual Feedback** - Watch each ass get consumed piece by piece
- **Sound Effects** - Different crunch sounds for each type
- **Score Tracking** - Local storage keeps your high scores
- **Responsive Design** - Works on desktop and mobile

## 🚀 Quick Start (For Players)

Just visit **[eatmyentireass.online](https://eatmyentireass.online)** and start clicking! No installation required.

## 🛠️ Development Setup

Want to contribute to this masterpiece of modern web development? Here's how to get started:

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (recommended) or [Visual Studio Code](https://code.visualstudio.com/)
- [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local) (for API development)

### Development with Visual Studio 2022

1. Clone the repository:
   ```bash
   git clone https://github.com/jwp528/eatmyentireass.online.git
   cd eatmyentireass.online
   ```

2. Open `EMEAOnline.sln` in Visual Studio 2022

3. Set up multiple startup projects:
   - Right-click the solution → **Configure Startup Projects**
   - Select **Multiple startup projects**
   - Set both **Api** and **Client** to **Start**

4. Press **F5** to launch both the client and API

5. Navigate to the displayed URL (typically `https://localhost:5001`)

### Development with Visual Studio Code + Azure SWA CLI

1. Clone and navigate to the repository (same as above)

2. Install required tools:
   ```bash
   npm install -g @azure/static-web-apps-cli
   npm install -g azure-functions-core-tools
   ```

3. Open the folder in VS Code

4. Start the development servers:

   **Terminal 1** - Start the Blazor client:
   ```bash
   cd Client
   dotnet run
   ```

   **Terminal 2** - Start the Azure Functions API:
   ```bash
   cd Api
   func start
   ```

   **Terminal 3** - Start the SWA CLI proxy:
   ```bash
   swa start http://localhost:5000 --api-location http://localhost:7071
   ```

5. Open your browser to `http://localhost:4280`

### Project Structure

- **Client/** - Blazor WebAssembly frontend with the game UI and logic
- **Api/** - Azure Functions backend API (currently minimal, ready for features like leaderboards)
- **Shared/** - Common models and enums shared between client and API

## 🎯 Contributing

We welcome contributions to make this game even more absurdly entertaining! Whether it's:

- New ass types with unique mechanics
- Visual improvements and animations  
- Sound effect enhancements
- Performance optimizations
- New game modes
- Leaderboard functionality

Please feel free to submit issues and pull requests.

## 🚀 Deployment

This application is designed to be deployed to [Azure Static Web Apps](https://docs.microsoft.com/azure/static-web-apps). The main branch automatically deploys to the production site at [eatmyentireass.online](https://eatmyentireass.online).

## ⚖️ Legal & Content Warning

This is a satirical web application intended for mature audiences. The content is intentionally absurd and not meant to be taken seriously. Please consume responsibly.

---

*Built with ❤️ and questionable humor using .NET 9, Blazor WebAssembly, and Azure Static Web Apps.*
