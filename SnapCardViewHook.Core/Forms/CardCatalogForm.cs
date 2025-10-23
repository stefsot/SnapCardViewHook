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

        private void CardCatalogForm_Load(object sender, EventArgs e)
        {
            var data = new BindingList<SnapCardDto>
            (
                SnapCardDefList.Cards.Where(c => c.IsObtainable()).OrderBy(c => c.GetEarliestEnabledDate()).Concat(
                SnapCardDefList.Cards.Where(c => !c.IsObtainable()).OrderBy(c => c.GetId()))
                .Select(c => new SnapCardDto(c))
                .ToList()
            );
            
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
                DataPropertyName = "Description",
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
                DataPropertyName = "Tokens",
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

            //dataGridView1.EnableHeadersVisualStyles = false;
            //dataGridView1.DefaultCellStyle.SelectionBackColor = dataGridView1.DefaultCellStyle.BackColor;
            //dataGridView1.DefaultCellStyle.SelectionForeColor = dataGridView1.DefaultCellStyle.ForeColor;
            //dataGridView1.RowHeadersDefaultCellStyle.SelectionBackColor = dataGridView1.RowHeadersDefaultCellStyle.BackColor;
            //dataGridView1.DefaultCellStyle.SelectionBackColor = Color.DarkCyan;


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
            private string _description;
            private int _cost;
            private int _power;
            private DateTime _releaseDate;
            private string _obtainable;
            private string _tokens;
            
            public SnapCardDto(CardDefWrapper cardDef)
            {
                _id = cardDef.GetId();
                _name = cardDef.Name;
                _cost = cardDef.Cost;   
                _power = cardDef.Power;
                _releaseDate = cardDef.GetEarliestEnabledDate();
                _obtainable = cardDef.IsObtainable() ? "Yes" : "No";
                _tokens = string.Join(", ", cardDef.GetTokens());

                var attributes = cardDef.GetAttributes();

                _description = FormatDescription(cardDef.Description, attributes);
                _description = StringHelper.CleanupHmtl(_description);
            }

            private string FormatDescription(string description, Dictionary<string, int[]> attributes)
            {
                return Regex.Replace(description, @"\{card\.(\w+)\}", match =>
                {
                    var attr = match.Groups[1].Value;
                    var indexes = attr.Split('_');
                    var index = 0;

                    if(indexes.Length > 1)
                    {
                        if (int.TryParse(indexes[1], out index))
                        {
                            index--;
                            attr = indexes[0];
                        }
                    }

                    if (!attributes.TryGetValue(attr, out var value))
                        return match.Value;

                    if (value.Length == 0)
                        return match.Value;

                    if (index >= value.Length)
                        return $"(!format error!) {match.Value}";

                    return value[index].ToString();
                });
            }

            public string Id => _id;
            public string Name => _name;
            public string Description => _description;
            public int Cost => _cost;
            public int Power => _power;
            public DateTime ReleaseDate => _releaseDate;
            public string IsObtainable => _obtainable;
            public string Tokens => _tokens;
        }
    }
}
