# EZPlay Mod for Oxygen Not Included

This mod provides a powerful external API to interact with and control the game "Oxygen Not Included" in real-time. It is designed to allow external tools, scripts, and AI agents to monitor game state, execute actions, and manage complex tasks like blueprint placement.

## Features

- **Dual API Servers**:
  - **HTTP Server**: Exposes a local HTTP server to receive commands and send game data on demand.
  - **WebSocket Server**: Provides a real-time event stream, pushing notifications for key game events as they happen.
- **Comprehensive Game State Monitoring**: Provides real-time access to detailed game state, including duplicant status, chores, grid data, resources, and game object information across multiple worlds.
- **Advanced Blueprint System**: Allows for the creation, saving, loading, and programmatic placement of complex building layouts (blueprints).
- **Dynamic Action Execution**: Supports executing global game actions (e.g., pause, screenshot) and invoking methods on any game object via a secure reflection-based system.
- **Security First**: Includes a robust, configurable whitelist to strictly control which game components and methods are accessible via the API, ensuring game stability.
- **Thread-Safe Operations**: Utilizes a main thread dispatcher to ensure all game-modifying operations are executed safely on the game's main thread.

## Project Structure

The project is organized into the following namespaces and directories, following the Single Responsibility Principle:

- **`Core`**: Contains the main mod loader, security whitelist, and other essential components.

  - `ModLoader.cs`: The entry point of the mod, responsible for initializing the API servers and applying Harmony patches.
  - `SecurityWhitelist.cs`: Defines the security boundaries of the API, specifying exactly which game components, methods, and properties are accessible.

- **`API`**: Manages the HTTP and WebSocket API servers.

  - `ApiServer.cs`: Manages the HTTP server for request-response interactions.
  - `EventSocketServer.cs`: Manages the WebSocket server for real-time event broadcasting.
  - `RequestHandler.cs`: The central hub for processing all API requests, routing them to the appropriate executors or query handlers.
  - **`Executors`**: Handles API requests that perform actions in the game.
  - **`Queries`**: Handles API requests that retrieve data from the game.

- **`Blueprints`**: Contains the logic for creating, managing, and placing building blueprints.

- **`GameState`**: Manages the overall state of the game world and provides data to the API.

- **`Patches`**: Holds all Harmony patches used to hook into the game's core logic.

  - `ResearchEventPatch.cs`: Triggers an event when a research task is completed.
  - `DuplicantEventPatch.cs`: Triggers an event when a duplicant dies.

- **`Utils`**: Provides utility classes used across the project.

## API Endpoints

The API server listens on `http://localhost:8080`. All endpoints are prefixed with `/api`.

| Method | Endpoint         | Description                                               | Example Payload                                                           |
| :----- | :--------------- | :-------------------------------------------------------- | :------------------------------------------------------------------------ |
| `GET`  | `/api/state`     | Retrieves the last known comprehensive game state.        | N/A                                                                       |
| `POST` | `/api/query`     | Executes a data query (e.g., grid, chores, find_objects). | `{"QueryType": "Grid", "Payload": {"cells": [12345]}}`                    |
| `POST` | `/api/execute`   | Executes an action (e.g., global, reflection, destroy).   | `{"Executor": "Global", "Payload": {"ActionName": "pause_game"}}`         |
| `POST` | `/api/blueprint` | Manages blueprints (create, place).                       | `{"Action": "Place", "Payload": {"name": "MyBase", "anchorCell": 12345}}` |

_For detailed payload structures for each query and action, please refer to the source code in the `API/Queries` and `API/Executors` directories._

## WebSocket Events

The WebSocket server listens on `ws://0.0.0.0:8081`. Connect to this URL to receive real-time event notifications from the game.

### Event Format

All events are sent as a JSON string. The `EventType` field identifies the type of event.

### Implemented Events

| EventType          | Description                              | Data Payload                                                              |
| :----------------- | :--------------------------------------- | :------------------------------------------------------------------------ |
| `ResearchComplete` | Fired when a research task is completed. | `{ "EventType": "ResearchComplete", "TechId": "...", "TechName": "..." }` |
| `DuplicantDeath`   | Fired when a duplicant dies.             | `{ "EventType": "DuplicantDeath", "DuplicantName": "..." }`               |

## How to Build

1.  **Prerequisites**:

    - .NET Framework 4.7.2 SDK.
    - Your "Oxygen Not Included" installation directory.

2.  **Setup**:

    - Copy all required DLLs from your game's `OxygenNotIncluded_Data/Managed` directory into the `MOd/lib/Managed` directory.
    - Run `dotnet restore` in the `MOd` directory to download the `WebSocketSharp-netstandard` dependency.

3.  **Build Command**:

    - Open a terminal in the `MOd` directory.
    - Run `dotnet build`.

4.  **Output**:

    - The compiled `EZPlay.dll` will be located in the `bin/Debug/net472` directory.

5.  **Installation**:
    - Copy the `EZPlay.dll` file to the `mods` folder in your "Oxygen Not Included" local files directory.

## Contributing

Contributions are welcome! Please feel free to submit a pull request or open an issue for any bugs or feature requests.

## License

This project is licensed under the MIT License.
