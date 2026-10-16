// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

namespace Planetsmith.Generation;

/// <summary>
/// One stage of the world-generation pipeline. Passes run in order, each
/// reading and refining the shared <see cref="GenerationContext"/> and the tiles on
/// its layer. Keeping stages behind this interface lets the climate model grow
/// from a couple of passes into the full dependency-ordered layer set.
/// </summary>
public interface IGenerationPass
{
	/// <summary>The pass's name in the generation log.</summary>
	string Name { get; }
	
	void Run(GenerationContext context);
}
