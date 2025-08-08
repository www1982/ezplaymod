# EZPlay Mod for Oxygen Not Included

This mod provides an external API to interact with and control the game "Oxygen Not Included" in real-time. It is designed to allow external tools and scripts to monitor game state, execute actions, and manage complex tasks like blueprint placement.

## Features

- **External API Server**: Exposes a local HTTP server to receive commands and send game data.
- **Game State Monitoring**: Provides real-time access to game state, including duplicant chores, grid data, and game object information.
- **Blueprint Placement**: Allows for the programmatic placement of complex building layouts (blueprints).
- **Dynamic Action Execution**: Supports executing global game actions and invoking methods on game objects via reflection.
- **Security**: Includes a whitelist to control which external clients can connect to the API.

## Project Structure

The project is organized into the following namespaces and directories, following the Single Responsibility Principle:

- **`Core`**: Contains the main mod loader, security whitelist, and other core components.
- **`API`**: Manages the HTTP API server.
  - **`Executors`**: Handles API requests that perform actions in the game (e.g., global actions, reflection).
  - **`Queries`**: Handles API requests that retrieve data from the game (e.g., grid data, chore status).
- **`Blueprints`**: Contains the logic for parsing and placing building blueprints.
- **`GameState`**: Manages the overall state of the game world and provides data to the API.
- **`Patches`**: Holds all Harmony patches used to hook into the game's core logic.
- **`Utils`**: Provides utility classes, such as the `MainThreadDispatcher` for executing code on the game's main thread.

## API Endpoints

The API server listens on `http://localhost:8080`. All endpoints are prefixed with `/api`.

| Method | Endpoint                     | Description                                                              |
| :----- | :--------------------------- | :----------------------------------------------------------------------- |
| `GET`  | `/api/state`                 | Retrieves the current game state, including duplicant and building info. |
| `GET`  | `/api/find_objects`          | Finds game objects based on specified criteria.                          |
| `GET`  | `/api/grid`                  | Queries for data about the game grid at a specific location.             |
| `GET`  | `/api/pathfinding`           | Performs a pathfinding query between two points.                         |
| `GET`  | `/api/chore_status`          | Gets the status of a specific duplicant's chores.                        |
| `POST` | `/api/execute_global_action` | Executes a global game action (e.g., taking a screenshot).               |
| `POST` | `/api/execute_reflection`    | Invokes a method or accesses a property on a game object via reflection. |
| `POST` | `/api/place_blueprint`       | Places a blueprint from a provided JSON structure.                       |
| `POST` | `/api/destroy_building`      | Destroys a building at a specified location.                             |

## How to Build

1.  **Prerequisites**:
    - .NET Framework 4.7.2 SDK.
    - Reference to `Assembly-CSharp.dll`, `0Harmony.dll`, and other required game libraries from your "Oxygen Not Included" installation directory.
2.  **Build Command**:
    - Open a terminal in the `MOd` directory.
    - Run `dotnet build` or build the `EZPlay.csproj` project using an IDE like Visual Studio.
3.  **Output**:
    - The compiled `EZPlay.dll` will be located in the `bin/Debug/net472` (or `bin/Release/net472`) directory.
4.  **Installation**:
    - Copy the `EZPlay.dll` file to the `mods` folder in your "Oxygen Not Included" local files.
