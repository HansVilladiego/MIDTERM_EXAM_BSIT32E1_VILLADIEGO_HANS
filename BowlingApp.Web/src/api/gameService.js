// =============================================================================================
// API Service - Connects Frontend to .NET Backend
// =============================================================================================

const API_BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5035/api/game";

export const createGame = async (playerNames) => {
    try {
        const response = await fetch(`${API_BASE_URL}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify(playerNames)
        });

        if (!response.ok) {
            const error = await response.text();
            throw new Error(`Failed to create game: ${error}`);
        }

        return await response.json();
    } catch (error) {
        console.error('Error creating game:', error);
        throw error;
    }
};

export const getGame = async (gameId) => {
    try {
        const response = await fetch(`${API_BASE_URL}/${gameId}`, {
            method: 'GET',
            headers: {
                'Accept': 'application/json'
            }
        });

        if (!response.ok) {
            const error = await response.text();
            throw new Error(`Failed to get game: ${error}`);
        }

        return await response.json();
    } catch (error) {
        console.error('Error getting game:', error);
        throw error;
    }
};

export const rollBall = async (gameId, playerId, pins) => {
    try {
        const response = await fetch(`${API_BASE_URL}/${gameId}/roll`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify({ playerId, pins })
        });

        if (!response.ok) {
            const error = await response.text();
            throw new Error(`Failed to roll ball: ${error}`);
        }

        return await response.json();
    } catch (error) {
        console.error('Error rolling ball:', error);
        throw error;
    }
};