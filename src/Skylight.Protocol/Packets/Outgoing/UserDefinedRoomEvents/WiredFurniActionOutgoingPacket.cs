﻿using System.Diagnostics.CodeAnalysis;
using Skylight.Protocol.Packets.Data.UserDefinedRoomEvents;

namespace Skylight.Protocol.Packets.Outgoing.UserDefinedRoomEvents;

public sealed class WiredFurniActionOutgoingPacket : IGameOutgoingPacket
{
	public required int ItemId { get; init; }
	public required int FurnitureId { get; init; }
	public required ActionType Type { get; init; }

	public required int MaxSelectedItems { get; init; }
	public required List<int> SelectedItems { get; init; }

	public required int Delay { get; init; }

	public required List<int> IntegerParameters { get; init; }
	public required string StringParameter { get; init; }

	public WiredFurniActionOutgoingPacket()
	{
	}

	[SetsRequiredMembers]
	public WiredFurniActionOutgoingPacket(int itemId, int furnitureId, ActionType type, int maxSelectedItems, List<int> selectedItems, int delay, List<int> integerParameters, string stringParameter)
	{
		this.ItemId = itemId;
		this.FurnitureId = furnitureId;
		this.Type = type;
		this.MaxSelectedItems = maxSelectedItems;
		this.SelectedItems = selectedItems;
		this.Delay = delay;
		this.IntegerParameters = integerParameters;
		this.StringParameter = stringParameter;
	}
}
