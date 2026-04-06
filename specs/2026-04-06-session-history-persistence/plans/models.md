# Session History Models

Because the app uses Agents Framework message types at runtime, no `ConversationSession` / `ConversationMessage` app models are required for the repository contract.

The repository stores raw JSON payloads as strings by `fileName`, and the service layer maps `sessionId` to file names while handling serialization/deserialization to framework message types.

No additional persistence models are required for this plan.
