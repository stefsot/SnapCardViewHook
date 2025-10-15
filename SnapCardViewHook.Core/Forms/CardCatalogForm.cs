using SnapCardViewHook.Core.Data;
using SnapCardViewHook.Core.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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
                HeaderText = "Is Obtainable",
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
            dataGridView1.DefaultCellStyle.SelectionBackColor = Color.DarkCyan;


            dataGridView1.AutoResizeColumns();
            dataGridView1.AutoResizeRows();
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView1.Columns[e.ColumnIndex] is DataGridViewButtonColumn)
            {
                var c = (SnapCardDto)dataGridView1.Rows[e.RowIndex].DataBoundItem;
                _parent.SetCardOverride(c.GetCardDef().GetId());
            }
        }

        private class SnapCardDto
        {
            private CardDefWrapper _cardDef;

            public SnapCardDto(CardDefWrapper cardDef)
            {
                _cardDef = cardDef;
            }

            public CardDefWrapper GetCardDef()
            { 
                return _cardDef; 
            }

            public string Id => _cardDef.GetId();
            public string Name => _cardDef.Name;
            public string Description => _cardDef.Description;
            public int Cost => _cardDef.Cost;
            public int Power => _cardDef.Power;

            public DateTime ReleaseDate => _cardDef.GetEarliestEnabledDate();
            public string IsObtainable => _cardDef.IsObtainable() ? "Yes" : "No";
        }
    }
}
