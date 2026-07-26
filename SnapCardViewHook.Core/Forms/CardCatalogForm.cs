using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SnapCardViewHook.Core.Data;
using SnapCardViewHook.Core.Helpers;
using SnapCardViewHook.Core.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace SnapCardViewHook.Core.Forms
{
    public partial class CardCatalogForm : Form
    {
        private CardViewSelectorForm _parent;

        public CardCatalogForm(CardViewSelectorForm parent)
        {
            _parent = parent;
            InitializeComponent();
        }

        SnapCardDto[] _cards;

        private void CardCatalogForm_Load(object sender, EventArgs e)
        {
            _cards = SnapCardDefList.Cards.Where(c => c.IsObtainable()).OrderBy(c => c.GetEarliestEnabledDate()).Concat(
                SnapCardDefList.Cards.Where(c => !c.IsObtainable()).OrderBy(c => c.GetId()))
                .Select(c => new SnapCardDto(c))
                .ToArray();

            var data = new BindingList<SnapCardDto>(_cards);
            
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.DataSource = data;

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Id",
                DataPropertyName = "Id"
            });
            //dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            //{
            //    HeaderText = "Name",
            //    DataPropertyName = "Name"
            //});
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Cost",
                DataPropertyName = "Cost"
            });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Power",
                DataPropertyName = "Power"
            });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Description",
                DataPropertyName = "DescriptionFormatted",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = 
                { 
                    WrapMode = DataGridViewTriState.True,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold)
                }
            });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 70,
                HeaderText = "Tokens",
                DataPropertyName = "TokensString",
                DefaultCellStyle =
                {
                    WrapMode = DataGridViewTriState.True,
                }
            });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Obtainable",
                DataPropertyName = "IsObtainable"
            });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Release Date",
                DataPropertyName = "ReleaseDate"
            });

            var btnCol = new DataGridViewButtonColumn
            {
                HeaderText = "Set as override",
                Text = ">",
                UseColumnTextForButtonValue = true
            };
            dataGridView1.Columns.Add(btnCol);

            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;

            dataGridView1.AutoResizeColumns();
            dataGridView1.AutoResizeRows();
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView1.Columns[e.ColumnIndex] is DataGridViewButtonColumn)
            {
                var c = (SnapCardDto)dataGridView1.Rows[e.RowIndex].DataBoundItem;
                _parent.SetCardOverride(c.Id);
            }
        }

        private class SnapCardDto
        {
            private string _id;
            private string _name;
            private string _descriptionFormatted;
            private int _cost;
            private int _power;
            private DateTime _releaseDate;
            private bool _isObtainable;
            private string _obtainable;
            private string _tokensString;
            private string[] _tokens;
            private string _description;
            private Dictionary<string, int[]> _attributes;

            public SnapCardDto(CardDefWrapper cardDef)
            {
                _id = cardDef.GetId();
                _name = cardDef.Name;
                _cost = cardDef.Cost;   
                _power = cardDef.Power;
                _releaseDate = cardDef.GetEarliestEnabledDate();
                _isObtainable = cardDef.IsObtainable();
                _obtainable = _isObtainable ? "Yes" : "No";
                _tokens = cardDef.GetTokens();
                _tokensString = string.Join(", ", _tokens);

                var attributes = _attributes = cardDef.GetAttributes();

                _description = cardDef.Description;
                _descriptionFormatted = FormatDescription(_description, attributes).Replace("\n", " ");
                _descriptionFormatted = StringHelper.CleanupHmtl(_descriptionFormatted);
            }

            private string FormatDescription(string description, Dictionary<string, int[]> attributes)
            {
                return Regex.Replace(description, @"\{card\.(\w+(?:\.\w+)?)\}", match =>
                {
                    var path = match.Groups[1].Value.Split('.');

                    if (path.Length == 1)
                        return ResolveAttributeReference(path[0], attributes, match.Value);

                    if (path.Length == 2)
                        return ResolveTokenReference(path[0], path[1], match.Value);

                    return match.Value;
                });
            }

            private string ResolveTokenReference(string tokenReference, string propertyReference, string fallback)
            {
                ParseIndexedReference(tokenReference, out var tokenAttribute, out var tokenIndex);

                if (!string.Equals(tokenAttribute, "Card_Token", StringComparison.Ordinal))
                    return fallback;

                if (_tokens == null || tokenIndex < 0 || tokenIndex >= _tokens.Length)
                    return fallback;

                var token = SnapCardDefList.FindCard(_tokens[tokenIndex]);
                if (token == null)
                    return fallback;

                switch (propertyReference)
                {
                    case "Power":
                        return token.Power.ToString();
                    case "Cost":
                        return token.Cost.ToString();
                    case "Name":
                        return token.Name ?? fallback;
                    case "Id":
                    case "CardDefId":
                        return token.GetId() ?? fallback;
                    default:
                        return ResolveAttributeReference(
                            propertyReference,
                            token.GetAttributes(),
                            fallback);
                }
            }

            private static string ResolveAttributeReference(
                string attributeReference,
                IDictionary<string, int[]> attributes,
                string fallback)
            {
                ParseIndexedReference(attributeReference, out var attribute, out var index);

                if (!attributes.TryGetValue(attribute, out var values) || values.Length == 0)
                    return fallback;

                if (index < 0 || index >= values.Length)
                    return $"(!format error!) {fallback}";

                return values[index].ToString();
            }

            private static void ParseIndexedReference(string reference, out string name, out int index)
            {
                name = reference;
                index = 0;

                var separator = reference.LastIndexOf('_');
                if (separator < 0 || separator == reference.Length - 1)
                    return;

                if (!int.TryParse(reference.Substring(separator + 1), out var oneBasedIndex))
                    return;

                name = reference.Substring(0, separator);
                index = oneBasedIndex - 1;
            }

            [JsonProperty("cardDefId")]
            public string Id => _id;
            [JsonProperty("name")]
            public string Name => _name;
            [JsonProperty("description")]
            public string Description => _description;
            [JsonProperty("descriptionFormatted")]
            public string DescriptionFormatted => _descriptionFormatted;
            [JsonProperty("cost")]
            public int Cost => _cost;
            [JsonProperty("power")]
            public int Power => _power;
            [JsonProperty("releaseDate")]
            public DateTime ReleaseDate => _releaseDate;
            [JsonProperty("obtainable")]
            public bool Obtainable => _isObtainable;
            [JsonIgnore]
            public string IsObtainable => _obtainable;
            [JsonIgnore]
            public string TokensString => _tokensString;
            [JsonProperty("tokens")]
            public string[] Tokens => _tokens;
            [JsonProperty("attributes")]
            public Dictionary<string, int[]> Attributes => _attributes;
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var f = new SaveFileDialog()
            {
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = "json",
                AddExtension = true,
                Title = "Export marvel snap card catalog"
            })
            {
                if(f.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        System.IO.File.WriteAllText(f.FileName, JsonConvert.SerializeObject(_cards, Formatting.Indented));
                    }
                    catch(Exception x)
                    {
                        MessageBox.Show($"There was an error while trying to save the data at \"{f.FileName}\". \nError details:\n\n{x}");
                    }
                }
            }
        }
    }
}
