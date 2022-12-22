﻿using System.Text.Encodings.Web;
using System.Text.Json;
using Skylight.Protocol.Generator;
using Skylight.Protocol.Generator.Schema;
using Skylight.Protocol.Generator.Schema.Mapping;
using Skylight.Protocol.Packets.Incoming;
using Skylight.Protocol.Packets.Outgoing;

namespace Skylight.Protocol.Editor;

internal partial class ProtocolOverviewForm : Form
{
	private readonly string protocol;

	private readonly ProtocolSchema schema;

	private readonly List<Action> unregisterListeners;

	internal ProtocolOverviewForm(string protocol)
	{
		this.InitializeComponent();

		this.protocol = protocol;

		using (Stream stream = File.OpenRead(Path.Combine(this.protocol, "packets.json")))
		{
			this.schema = JsonSerializer.Deserialize<ProtocolSchema>(stream)!;
		}

		this.unregisterListeners = new List<Action>();
	}

	private void ProtocolOverviewFormLoad(object sender, EventArgs e)
	{
		this.protocolName.Text = Path.GetFileName(this.protocol);

		this.packetTabControl.Appearance = TabAppearance.FlatButtons;
		this.packetTabControl.SizeMode = TabSizeMode.Fixed;
		this.packetTabControl.ItemSize = new Size(0, 1);

		this.Text = $"Protocol {this.protocolName.Text}";

		foreach (string name in this.schema.Incoming.Keys)
		{
			this.incomingPacketList.Items.Add(name);
		}

		foreach (string name in this.schema.Outgoing.Keys)
		{
			this.outgoingPacketsList.Items.Add(name);
		}

		foreach (string name in this.schema.Structures.Keys)
		{
			this.structuresList.Items.Add(name);
		}

		foreach (string name in this.schema.Interfaces.Keys)
		{
			this.interfacesList.Items.Add(name);
		}
	}

	private void SelectIncomingPacket(object sender, EventArgs e)
	{
		this.packetTabControl.SelectedTab = this.packetTab;

		if (this.incomingPacketList.SelectedItems.Count != 1)
		{
			return;
		}

		string name = this.incomingPacketList.SelectedItems[0].Text;

		this.DisplayPacket(name, this.schema.Incoming[name], typeof(IGameIncomingPacket));
	}

	private void SelectOutgoingPacket(object sender, EventArgs e)
	{
		this.packetTabControl.SelectedTab = this.packetTab;

		if (this.outgoingPacketsList.SelectedItems.Count != 1)
		{
			return;
		}

		string name = this.outgoingPacketsList.SelectedItems[0].Text;

		this.DisplayPacket(name, this.schema.Outgoing[name], typeof(IGameOutgoingPacket));
	}

	private void SelectStructure(object sender, EventArgs e)
	{
		this.packetTabControl.SelectedTab = this.structureTab;

		if (this.structuresList.SelectedItems.Count != 1)
		{
			return;
		}

		this.VisualizePacketData(this.structureData, this.schema.Structures[this.structuresList.SelectedItems[0].Text], null);
	}

	private void SelectInterface(object sender, EventArgs e)
	{
		this.packetTabControl.SelectedTab = this.interfaceTab;

		if (this.interfacesList.SelectedItems.Count != 1)
		{
			return;
		}

		this.interfaceData.Controls.Clear();

		foreach ((string key, string value) in this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text])
		{
			TextBox keyTextBox = new()
			{
				Width = 500,
				Text = key
			};

			TextBox valueTextBox = new()
			{
				Width = 200,
				Text = value
			};

			Button removeButton = new()
			{
				Text = "X"
			};

			string oldKey = keyTextBox.Text;
			keyTextBox.TextChanged += (_, _) =>
			{
				this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text].Remove(oldKey);
				this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text][keyTextBox.Text] = valueTextBox.Text;

				oldKey = keyTextBox.Text;
			};

			valueTextBox.TextChanged += (_, _) =>
			{
				this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text][keyTextBox.Text] = valueTextBox.Text;
			};

			removeButton.Click += (_, _) =>
			{
				this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text].Remove(keyTextBox.Text);

				this.interfaceData.Controls.Remove(removeButton.Parent);
			};

			this.interfaceData.Controls.Add(new FlowLayoutPanel
			{
				Width = this.interfaceData.Width - 10,
				Height = 40,
				Controls =
				{
					keyTextBox,
					valueTextBox,
					removeButton
				}
			});
		}
	}

	private void DisplayPacket(string name, PacketSchema packet, Type interfaceType)
	{
		foreach (Action unregisterListener in this.unregisterListeners)
		{
			unregisterListener();
		}

		this.unregisterListeners.Clear();

		EventHandler eventHandler = (_, _) => packet.Id = (uint)this.packetId.Value;

		this.unregisterListeners.Add(() => this.packetId.ValueChanged -= eventHandler);

		this.packetId.Value = packet.Id;
		this.packetId.ValueChanged += eventHandler;

		int groupIdentifier = name.LastIndexOf('.');

		string packetGroup = name.Substring(0, groupIdentifier);
		string packetName = name.Substring(groupIdentifier + 1);

		Type? packetInterface;
		if (interfaceType == typeof(IGameIncomingPacket))
		{
			packetInterface = interfaceType.Assembly.GetType($"{interfaceType.Namespace}.{packetGroup}.I{packetName}IncomingPacket");
		}
		else if (interfaceType == typeof(IGameOutgoingPacket))
		{
			packetInterface = interfaceType.Assembly.GetType($"{interfaceType.Namespace}.{packetGroup}.{packetName}OutgoingPacket");
		}
		else
		{
			throw new Exception($"Unknown interface type {interfaceType}");
		}

		this.VisualizePacketData(this.packetData, packet.Structure, packetInterface!);
	}

	private void VisualizePacketData(FlowLayoutPanel panel, List<AbstractMappingSchema> structures, Type? packetInterface, bool clear = true, bool canRemove = true, List<AbstractMappingSchema>? listHook = null)
	{
		if (clear)
		{
			panel.Controls.Clear();
		}

		if (listHook is null)
		{
			listHook = structures;
		}

		string[] packetProperties = packetInterface?.GetProperties().Select(p => p.Name).ToArray() ?? Array.Empty<string>();

		foreach (AbstractMappingSchema structure in structures)
		{
			Control control;
			if (structure is FieldMappingSchema fieldMapping)
			{
				ComboBox nameMapping = new()
				{
					Width = 200,
					SelectedText = fieldMapping.Name
				};

				nameMapping.TextChanged += (_, _) => fieldMapping.Name = nameMapping.Text;

				foreach (string packetProperty in packetProperties)
				{
					nameMapping.Items.Add(packetProperty);
				}

				ComboBox typeMapping = new()
				{
					Width = 200,
					Items =
					{
						"string",
						"text",
						"int",
						"short",
						"bool"
					},
					SelectedText = fieldMapping.Type
				};

				typeMapping.TextChanged += (_, _) => fieldMapping.Type = typeMapping.Text;

				control = new FlowLayoutPanel
				{
					Width = panel.Width - 10,
					Height = 40,
					Controls =
					{
						nameMapping,
						typeMapping
					}
				};
			}
			else if (structure is ConstantMappingSchema constantMapping)
			{
				ComboBox typeMapping = new()
				{
					Width = 200,
					Items =
					{
						"string",
						"text",
						"int",
						"short",
						"bool"
					},
					SelectedText = constantMapping.Type
				};

				typeMapping.TextChanged += (_, _) => constantMapping.Type = typeMapping.Text;

				TextBox valueMapping = new()
				{
					Width = 200,
					Text = constantMapping.Value
				};

				valueMapping.TextChanged += (_, _) => constantMapping.Value = valueMapping.Text;

				control = new FlowLayoutPanel
				{
					Width = panel.Width - 10,
					Height = 40,
					Controls =
					{
						typeMapping,
						valueMapping
					}
				};
			}
			else if (structure is ConditionalMappingSchema conditionalMapping)
			{
				Label conditionalLabel = new()
				{
					Text = "When: ",
					Width = 50
				};

				TextBox conditionalTextBox = new()
				{
					Width = 500,
					Text = conditionalMapping.Condition
				};

				conditionalTextBox.TextChanged += (_, _) => conditionalMapping.Condition = conditionalTextBox.Text;

				FlowLayoutPanel conditionalLayout = new()
				{
					Location = new Point(0, 20),
					Width = panel.Width - 10,
					Controls =
					{
						conditionalLabel,
						conditionalTextBox,
					}
				};

				this.VisualizePacketData(conditionalLayout, new List<AbstractMappingSchema>
				{
					conditionalMapping.WhenTrue
				}, null, false, false);

				control = new GroupBox
				{
					Width = panel.Width - 10,
					Controls =
					{
						conditionalLayout
					}
				};
			}
			else if (structure is CombineMappingSchema combineMapping)
			{
				Label combineLabel = new()
				{
					Text = "Combine to: ",
					Width = 100
				};

				ComboBox typeMapping = new()
				{
					Width = 200,
					Items =
					{
						"string",
						"text",
						"int",
						"short",
						"bool"
					},
					SelectedText = combineMapping.Type
				};

				typeMapping.TextChanged += (_, _) => combineMapping.Type = typeMapping.Text;

				FlowLayoutPanel conditionalLayout = new()
				{
					Location = new Point(0, 20),
					Width = panel.Width - 10,
					Height = 50 * combineMapping.Fields.Count,
					Controls =
					{
						combineLabel,
						typeMapping
					}
				};

				this.VisualizePacketData(conditionalLayout, combineMapping.Fields, null, false);

				control = new GroupBox
				{
					Width = panel.Width - 10,
					Height = (50 * combineMapping.Fields.Count) + 40,
					Controls =
					{
						conditionalLayout
					}
				};
			}
			else
			{
				throw new NotSupportedException();
			}

			if (canRemove)
			{
				Button upButton = new()
				{
					Text = "^"
				};

				Button downButton = new()
				{
					Text = "v"
				};

				Button removeButton = new()
				{
					Text = "X"
				};

				upButton.Click += (_, _) =>
				{
					panel.Controls.SetChildIndex(control, panel.Controls.IndexOf(control) - 1);

					int index = listHook.IndexOf(structure);
					if (index == 0)
					{
						index = listHook.Count;
					}

					listHook.Remove(structure);
					listHook.Insert(index - 1, structure);
				};

				downButton.Click += (_, _) =>
				{
					panel.Controls.SetChildIndex(control, panel.Controls.IndexOf(control) + 1);

					int index = listHook.IndexOf(structure);
					if (index == listHook.Count - 1)
					{
						return;
					}

					listHook.Remove(structure);
					listHook.Insert(index + 1, structure);
				};

				removeButton.Click += (_, _) =>
				{
					listHook.Remove(structure);

					panel.Controls.Remove(control);
				};

				control.Controls.Add(new FlowLayoutPanel
				{
					Width = 300,
					Controls =
					{
						upButton,
						downButton,
						removeButton
					}
				});
			}

			panel.Controls.Add(control);
		}
	}

	private void AddPacketField(object sender, EventArgs e)
	{
		FieldMappingSchema mapping = new()
		{
			Name = "NewField",
			Type = "string"
		};

		List<AbstractMappingSchema> listHook = this.structuresTab.SelectedTab == this.incomingPacketsTab
			? this.schema.Incoming[this.incomingPacketList.SelectedItems[0].Text].Structure
			: this.schema.Outgoing[this.outgoingPacketsList.SelectedItems[0].Text].Structure;

		listHook.Add(mapping);

		this.VisualizePacketData(this.packetData, new List<AbstractMappingSchema>
		{
			mapping
		}, null, false, listHook: listHook);
	}

	private void AddPacketConstant(object sender, EventArgs e)
	{
		ConstantMappingSchema mapping = new()
		{
			Type = "string",
			Value = "New Constant"
		};

		List<AbstractMappingSchema> listHook = this.structuresTab.SelectedTab == this.incomingPacketsTab
			? this.schema.Incoming[this.incomingPacketList.SelectedItems[0].Text].Structure
			: this.schema.Outgoing[this.outgoingPacketsList.SelectedItems[0].Text].Structure;

		listHook.Add(mapping);

		this.VisualizePacketData(this.packetData, new List<AbstractMappingSchema>
		{
			mapping
		}, null, false, listHook: listHook);
	}

	private void AddPacketConditional(object sender, EventArgs e)
	{
		ConditionalMappingSchema mapping = new()
		{
			Condition = "true",

			WhenTrue = new FieldMappingSchema
			{
				Name = "NewField",
				Type = "string"
			}
		};

		List<AbstractMappingSchema> listHook = this.structuresTab.SelectedTab == this.incomingPacketsTab
			? this.schema.Incoming[this.incomingPacketList.SelectedItems[0].Text].Structure
			: this.schema.Outgoing[this.outgoingPacketsList.SelectedItems[0].Text].Structure;

		listHook.Add(mapping);

		this.VisualizePacketData(this.packetData, new List<AbstractMappingSchema>
		{
			mapping
		}, null, false, listHook: listHook);
	}

	private void AddStructureField(object sender, EventArgs e)
	{
		FieldMappingSchema mapping = new()
		{
			Name = "NewField",
			Type = "string"
		};

		this.VisualizePacketData(this.structureData, new List<AbstractMappingSchema>
		{
			mapping
		}, null, false, listHook: this.schema.Structures[this.structuresList.SelectedItems[0].Text]);

		this.schema.Structures[this.structuresList.SelectedItems[0].Text].Add(mapping);
	}

	private void AddStructureConstant(object sender, EventArgs e)
	{
		ConstantMappingSchema mapping = new()
		{
			Type = "string",
			Value = "New Constant"
		};

		this.VisualizePacketData(this.structureData, new List<AbstractMappingSchema>
		{
			mapping
		}, null, false, listHook: this.schema.Structures[this.structuresList.SelectedItems[0].Text]);

		this.schema.Structures[this.structuresList.SelectedItems[0].Text].Add(mapping);
	}

	private void AddStructureConditional(object sender, EventArgs e)
	{
		ConditionalMappingSchema mapping = new()
		{
			Condition = "true",

			WhenTrue = new FieldMappingSchema
			{
				Name = "NewField",
				Type = "string"
			}
		};

		this.VisualizePacketData(this.structureData, new List<AbstractMappingSchema>
		{
			mapping
		}, null, false, listHook: this.schema.Structures[this.structuresList.SelectedItems[0].Text]);

		this.schema.Structures[this.structuresList.SelectedItems[0].Text].Add(mapping);
	}

	private void AddInterface(object sender, EventArgs e)
	{
		TextBox keyTextBox = new()
		{
			Width = 500,
			Text = "New interface"
		};

		TextBox valueTextBox = new()
		{
			Width = 200,
			Text = "New value"
		};

		Button removeButton = new()
		{
			Text = "X"
		};

		string oldKey = keyTextBox.Text;
		keyTextBox.TextChanged += (_, _) =>
		{
			this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text].Remove(oldKey);
			this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text][keyTextBox.Text] = valueTextBox.Text;

			oldKey = keyTextBox.Text;
		};

		valueTextBox.TextChanged += (_, _) =>
		{
			this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text][keyTextBox.Text] = valueTextBox.Text;
		};

		removeButton.Click += (_, _) =>
		{
			this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text].Remove(keyTextBox.Text);

			this.interfaceData.Controls.Remove(removeButton.Parent);
		};

		this.interfaceData.Controls.Add(new FlowLayoutPanel
		{
			Width = this.interfaceData.Width - 10,
			Height = 40,
			Controls =
			{
				keyTextBox,
				valueTextBox,
				removeButton
			}
		});
	}

	private void Save(object sender, EventArgs e)
	{
		try
		{
			string packetsTempPath = Path.Combine(this.protocol, "packets.json.temp");

			using (Stream stream = File.OpenWrite(packetsTempPath))
			{
				JsonSerializer.Serialize(stream, this.schema, new JsonSerializerOptions
				{
					Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
					WriteIndented = true
				});
			}

			File.Move(packetsTempPath, Path.Combine(this.protocol, "packets.json"), true);

			ProtocolGenerator.Run(this.protocol, this.schema);
		}
		catch (Exception exception)
		{
			MessageBox.Show(exception.ToString());
		}
	}

	private void AddNew(object sender, EventArgs e)
	{
		string text = this.addNewTextBox.Text;

		if (this.structuresTab.SelectedTab == this.incomingPacketsTab)
		{
			if (!text.Contains('.'))
			{
				MessageBox.Show("Packets require group");

				return;
			}

			bool result = this.schema.Incoming.TryAdd(text, new PacketSchema
			{
				Structure = new List<AbstractMappingSchema>()
			});

			if (!result)
			{
				MessageBox.Show("Item already exists");

				return;
			}

			this.incomingPacketList.Items.Add(text, text, string.Empty);
		}
		else if (this.structuresTab.SelectedTab == this.outgoingPacketsTab)
		{
			if (!text.Contains('.'))
			{
				MessageBox.Show("Packets require group");

				return;
			}

			bool result = this.schema.Outgoing.TryAdd(text, new PacketSchema
			{
				Structure = new List<AbstractMappingSchema>()
			});

			if (!result)
			{
				MessageBox.Show("Item already exists");

				return;
			}

			this.outgoingPacketsList.Items.Add(text, text, string.Empty);
		}
		else if (this.structuresTab.SelectedTab == this.selectStructureTab)
		{
			if (!this.schema.Structures.TryAdd(text, new List<AbstractMappingSchema>()))
			{
				MessageBox.Show("Item already exists");

				return;
			}

			this.structuresList.Items.Add(text, text, string.Empty);
		}
		else if (this.structuresTab.SelectedTab == this.interfacesTab)
		{
			if (!this.schema.Interfaces.TryAdd(text, new SortedDictionary<string, string>()))
			{
				MessageBox.Show("Item already exists");

				return;
			}

			this.interfacesList.Items.Add(text, text, string.Empty);
		}

		this.addNewTextBox.Text = string.Empty;
	}

	private void KeyDownIncomingPackets(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Delete && this.incomingPacketList.SelectedItems.Count == 1)
		{
			this.schema.Incoming.Remove(this.incomingPacketList.SelectedItems[0].Text);

			this.incomingPacketList.Items.Remove(this.incomingPacketList.SelectedItems[0]);
		}
	}

	private void KeyDownOutgoingPacket(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Delete && this.outgoingPacketsList.SelectedItems.Count == 1)
		{
			this.schema.Outgoing.Remove(this.outgoingPacketsList.SelectedItems[0].Text);

			this.outgoingPacketsList.Items.Remove(this.outgoingPacketsList.SelectedItems[0]);
		}
	}

	private void KeyDownStructures(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Delete && this.structuresList.SelectedItems.Count == 1)
		{
			this.schema.Structures.Remove(this.structuresList.SelectedItems[0].Text);

			this.structuresList.Items.Remove(this.structuresList.SelectedItems[0]);
		}
	}

	private void KeyDownInterfaces(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Delete && this.interfacesList.SelectedItems.Count == 1)
		{
			this.schema.Interfaces.Remove(this.interfacesList.SelectedItems[0].Text);

			this.interfacesList.Items.Remove(this.interfacesList.SelectedItems[0]);
		}
	}
}