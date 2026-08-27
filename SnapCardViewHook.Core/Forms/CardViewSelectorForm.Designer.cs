namespace SnapCardViewHook.Core.Forms
{
    partial class CardViewSelectorForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.surfaceEffectBox = new System.Windows.Forms.ComboBox();
            this.overrideSurfaceEffectCheckBox = new System.Windows.Forms.CheckBox();
            this.overrideRevealEffectCheckBox = new System.Windows.Forms.CheckBox();
            this.revealEffectBox = new System.Windows.Forms.ComboBox();
            this.overrideVariantCheckBox = new System.Windows.Forms.CheckBox();
            this.variantBox = new System.Windows.Forms.ComboBox();
            this.ensureVariantMatchCheckbox = new System.Windows.Forms.CheckBox();
            this.force3DCheckbox = new System.Windows.Forms.CheckBox();
            this.overrideBorderCheckBox = new System.Windows.Forms.CheckBox();
            this.borderBox = new System.Windows.Forms.ComboBox();
            this.overrideCardCheckBox = new System.Windows.Forms.CheckBox();
            this.cardBox = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.flipCardCheckBox = new System.Windows.Forms.CheckBox();
            this.overrideCardBackCheckBox = new System.Windows.Forms.CheckBox();
            this.cardBackBox = new System.Windows.Forms.ComboBox();
            this.descriptionTextBox = new System.Windows.Forms.TextBox();
            this.overrideDescriptionCheckBox = new System.Windows.Forms.CheckBox();
            this.overrideBoardCheckBox = new System.Windows.Forms.CheckBox();
            this.boardBox = new System.Windows.Forms.ComboBox();
            this.showCatalogButton = new System.Windows.Forms.Button();
            this.overrideFactionCheckBox = new System.Windows.Forms.CheckBox();
            this.factionBox = new System.Windows.Forms.ComboBox();
            this.changeCostCheckBox = new System.Windows.Forms.CheckBox();
            this.changePowerCheckBox = new System.Windows.Forms.CheckBox();
            this.costNumeric = new System.Windows.Forms.NumericUpDown();
            this.powerNumeric = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.costNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.powerNumeric)).BeginInit();
            this.SuspendLayout();
            // 
            // surfaceEffectBox
            // 
            this.surfaceEffectBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.surfaceEffectBox.FormattingEnabled = true;
            this.surfaceEffectBox.Location = new System.Drawing.Point(34, 114);
            this.surfaceEffectBox.Name = "surfaceEffectBox";
            this.surfaceEffectBox.Size = new System.Drawing.Size(216, 21);
            this.surfaceEffectBox.TabIndex = 0;
            // 
            // overrideSurfaceEffectCheckBox
            // 
            this.overrideSurfaceEffectCheckBox.AutoSize = true;
            this.overrideSurfaceEffectCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.overrideSurfaceEffectCheckBox.Location = new System.Drawing.Point(34, 88);
            this.overrideSurfaceEffectCheckBox.Name = "overrideSurfaceEffectCheckBox";
            this.overrideSurfaceEffectCheckBox.Size = new System.Drawing.Size(160, 20);
            this.overrideSurfaceEffectCheckBox.TabIndex = 1;
            this.overrideSurfaceEffectCheckBox.Text = "Override surface effect";
            this.overrideSurfaceEffectCheckBox.UseVisualStyleBackColor = true;
            // 
            // overrideRevealEffectCheckBox
            // 
            this.overrideRevealEffectCheckBox.AutoSize = true;
            this.overrideRevealEffectCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.overrideRevealEffectCheckBox.Location = new System.Drawing.Point(34, 163);
            this.overrideRevealEffectCheckBox.Name = "overrideRevealEffectCheckBox";
            this.overrideRevealEffectCheckBox.Size = new System.Drawing.Size(154, 20);
            this.overrideRevealEffectCheckBox.TabIndex = 3;
            this.overrideRevealEffectCheckBox.Text = "Override reveal effect";
            this.overrideRevealEffectCheckBox.UseVisualStyleBackColor = true;
            // 
            // revealEffectBox
            // 
            this.revealEffectBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.revealEffectBox.FormattingEnabled = true;
            this.revealEffectBox.Location = new System.Drawing.Point(34, 189);
            this.revealEffectBox.Name = "revealEffectBox";
            this.revealEffectBox.Size = new System.Drawing.Size(216, 21);
            this.revealEffectBox.TabIndex = 2;
            // 
            // overrideVariantCheckBox
            // 
            this.overrideVariantCheckBox.AutoSize = true;
            this.overrideVariantCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.overrideVariantCheckBox.Location = new System.Drawing.Point(34, 237);
            this.overrideVariantCheckBox.Name = "overrideVariantCheckBox";
            this.overrideVariantCheckBox.Size = new System.Drawing.Size(121, 20);
            this.overrideVariantCheckBox.TabIndex = 5;
            this.overrideVariantCheckBox.Text = "Override variant";
            this.overrideVariantCheckBox.UseVisualStyleBackColor = true;
            // 
            // variantBox
            // 
            this.variantBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.variantBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.variantBox.FormattingEnabled = true;
            this.variantBox.Location = new System.Drawing.Point(34, 284);
            this.variantBox.Name = "variantBox";
            this.variantBox.Size = new System.Drawing.Size(216, 21);
            this.variantBox.TabIndex = 4;
            // 
            // ensureVariantMatchCheckbox
            // 
            this.ensureVariantMatchCheckbox.AutoSize = true;
            this.ensureVariantMatchCheckbox.Checked = true;
            this.ensureVariantMatchCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ensureVariantMatchCheckbox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ensureVariantMatchCheckbox.Location = new System.Drawing.Point(34, 258);
            this.ensureVariantMatchCheckbox.Name = "ensureVariantMatchCheckbox";
            this.ensureVariantMatchCheckbox.Size = new System.Drawing.Size(195, 20);
            this.ensureVariantMatchCheckbox.TabIndex = 6;
            this.ensureVariantMatchCheckbox.Text = "Ensure variant matches card";
            this.ensureVariantMatchCheckbox.UseVisualStyleBackColor = true;
            // 
            // force3DCheckbox
            // 
            this.force3DCheckbox.AutoSize = true;
            this.force3DCheckbox.Checked = true;
            this.force3DCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.force3DCheckbox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.force3DCheckbox.Location = new System.Drawing.Point(34, 549);
            this.force3DCheckbox.Name = "force3DCheckbox";
            this.force3DCheckbox.Size = new System.Drawing.Size(111, 20);
            this.force3DCheckbox.TabIndex = 7;
            this.force3DCheckbox.Text = "Force 3D card";
            this.force3DCheckbox.UseVisualStyleBackColor = true;
            // 
            // overrideBorderCheckBox
            // 
            this.overrideBorderCheckBox.AutoSize = true;
            this.overrideBorderCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.overrideBorderCheckBox.Location = new System.Drawing.Point(34, 342);
            this.overrideBorderCheckBox.Name = "overrideBorderCheckBox";
            this.overrideBorderCheckBox.Size = new System.Drawing.Size(121, 20);
            this.overrideBorderCheckBox.TabIndex = 9;
            this.overrideBorderCheckBox.Text = "Override border";
            this.overrideBorderCheckBox.UseVisualStyleBackColor = true;
            // 
            // borderBox
            // 
            this.borderBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.borderBox.FormattingEnabled = true;
            this.borderBox.Location = new System.Drawing.Point(34, 368);
            this.borderBox.Name = "borderBox";
            this.borderBox.Size = new System.Drawing.Size(216, 21);
            this.borderBox.TabIndex = 8;
            // 
            // overrideCardCheckBox
            // 
            this.overrideCardCheckBox.AutoSize = true;
            this.overrideCardCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.overrideCardCheckBox.Location = new System.Drawing.Point(34, 11);
            this.overrideCardCheckBox.Name = "overrideCardCheckBox";
            this.overrideCardCheckBox.Size = new System.Drawing.Size(108, 20);
            this.overrideCardCheckBox.TabIndex = 11;
            this.overrideCardCheckBox.Text = "Replace card";
            this.overrideCardCheckBox.UseVisualStyleBackColor = true;
            // 
            // cardBox
            // 
            this.cardBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cardBox.FormattingEnabled = true;
            this.cardBox.Location = new System.Drawing.Point(34, 37);
            this.cardBox.Name = "cardBox";
            this.cardBox.Size = new System.Drawing.Size(216, 21);
            this.cardBox.TabIndex = 10;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.Color.Red;
            this.label1.Location = new System.Drawing.Point(16, 308);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(261, 13);
            this.label1.TabIndex = 12;
            this.label1.Text = "!! setting an invalid variant value will crash the game !!";
            // 
            // flipCardCheckBox
            // 
            this.flipCardCheckBox.AutoSize = true;
            this.flipCardCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.flipCardCheckBox.Location = new System.Drawing.Point(151, 549);
            this.flipCardCheckBox.Name = "flipCardCheckBox";
            this.flipCardCheckBox.Size = new System.Drawing.Size(78, 20);
            this.flipCardCheckBox.TabIndex = 13;
            this.flipCardCheckBox.Text = "Flip card";
            this.flipCardCheckBox.UseVisualStyleBackColor = true;
            this.flipCardCheckBox.CheckedChanged += new System.EventHandler(this.flipCardCheckBox_CheckedChanged_1);
            // 
            // overrideCardBackCheckBox
            // 
            this.overrideCardBackCheckBox.AutoSize = true;
            this.overrideCardBackCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.overrideCardBackCheckBox.Location = new System.Drawing.Point(34, 412);
            this.overrideCardBackCheckBox.Name = "overrideCardBackCheckBox";
            this.overrideCardBackCheckBox.Size = new System.Drawing.Size(141, 20);
            this.overrideCardBackCheckBox.TabIndex = 15;
            this.overrideCardBackCheckBox.Text = "Override card back";
            this.overrideCardBackCheckBox.UseVisualStyleBackColor = true;
            // 
            // cardBackBox
            // 
            this.cardBackBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cardBackBox.FormattingEnabled = true;
            this.cardBackBox.Location = new System.Drawing.Point(34, 438);
            this.cardBackBox.Name = "cardBackBox";
            this.cardBackBox.Size = new System.Drawing.Size(216, 21);
            this.cardBackBox.TabIndex = 14;
            // 
            // descriptionTextBox
            // 
            this.descriptionTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.descriptionTextBox.Location = new System.Drawing.Point(299, 114);
            this.descriptionTextBox.Multiline = true;
            this.descriptionTextBox.Name = "descriptionTextBox";
            this.descriptionTextBox.Size = new System.Drawing.Size(216, 96);
            this.descriptionTextBox.TabIndex = 16;
            this.descriptionTextBox.Text = "<b>Ongoing:</b> Test <color=#ff2c2c>text</color> change.";
            this.descriptionTextBox.TextChanged += new System.EventHandler(this.descriptionTextBox_TextChanged);
            // 
            // overrideDescriptionCheckBox
            // 
            this.overrideDescriptionCheckBox.AutoSize = true;
            this.overrideDescriptionCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.overrideDescriptionCheckBox.Location = new System.Drawing.Point(299, 88);
            this.overrideDescriptionCheckBox.Name = "overrideDescriptionCheckBox";
            this.overrideDescriptionCheckBox.Size = new System.Drawing.Size(131, 20);
            this.overrideDescriptionCheckBox.TabIndex = 17;
            this.overrideDescriptionCheckBox.Text = "Replace card text";
            this.overrideDescriptionCheckBox.UseVisualStyleBackColor = true;
            this.overrideDescriptionCheckBox.CheckedChanged += new System.EventHandler(this.overrideDescriptionCheckBox_CheckedChanged);
            // 
            // overrideBoardCheckBox
            // 
            this.overrideBoardCheckBox.AutoSize = true;
            this.overrideBoardCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.overrideBoardCheckBox.Location = new System.Drawing.Point(299, 237);
            this.overrideBoardCheckBox.Name = "overrideBoardCheckBox";
            this.overrideBoardCheckBox.Size = new System.Drawing.Size(155, 20);
            this.overrideBoardCheckBox.TabIndex = 18;
            this.overrideBoardCheckBox.Text = "Override game board";
            this.overrideBoardCheckBox.UseVisualStyleBackColor = true;
            // 
            // boardBox
            // 
            this.boardBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.boardBox.FormattingEnabled = true;
            this.boardBox.Location = new System.Drawing.Point(299, 263);
            this.boardBox.Name = "boardBox";
            this.boardBox.Size = new System.Drawing.Size(216, 21);
            this.boardBox.TabIndex = 19;
            // 
            // showCatalogButton
            // 
            this.showCatalogButton.Location = new System.Drawing.Point(403, 546);
            this.showCatalogButton.Name = "showCatalogButton";
            this.showCatalogButton.Size = new System.Drawing.Size(127, 23);
            this.showCatalogButton.TabIndex = 20;
            this.showCatalogButton.Text = "Show card catalog";
            this.showCatalogButton.UseVisualStyleBackColor = true;
            this.showCatalogButton.Click += new System.EventHandler(this.showCatalogButton_Click);
            // 
            // overrideFactionCheckBox
            // 
            this.overrideFactionCheckBox.AutoSize = true;
            this.overrideFactionCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.overrideFactionCheckBox.Location = new System.Drawing.Point(34, 480);
            this.overrideFactionCheckBox.Name = "overrideFactionCheckBox";
            this.overrideFactionCheckBox.Size = new System.Drawing.Size(120, 20);
            this.overrideFactionCheckBox.TabIndex = 22;
            this.overrideFactionCheckBox.Text = "Override faction";
            this.overrideFactionCheckBox.UseVisualStyleBackColor = true;
            // 
            // factionBox
            // 
            this.factionBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.factionBox.FormattingEnabled = true;
            this.factionBox.Location = new System.Drawing.Point(34, 506);
            this.factionBox.Name = "factionBox";
            this.factionBox.Size = new System.Drawing.Size(216, 21);
            this.factionBox.TabIndex = 21;
            // 
            // changeCostCheckBox
            // 
            this.changeCostCheckBox.AutoSize = true;
            this.changeCostCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.changeCostCheckBox.Location = new System.Drawing.Point(299, 11);
            this.changeCostCheckBox.Name = "changeCostCheckBox";
            this.changeCostCheckBox.Size = new System.Drawing.Size(96, 20);
            this.changeCostCheckBox.TabIndex = 23;
            this.changeCostCheckBox.Text = "Modify Cost";
            this.changeCostCheckBox.UseVisualStyleBackColor = true;
            // 
            // changePowerCheckBox
            // 
            this.changePowerCheckBox.AutoSize = true;
            this.changePowerCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.changePowerCheckBox.Location = new System.Drawing.Point(417, 11);
            this.changePowerCheckBox.Name = "changePowerCheckBox";
            this.changePowerCheckBox.Size = new System.Drawing.Size(107, 20);
            this.changePowerCheckBox.TabIndex = 24;
            this.changePowerCheckBox.Text = "Modify Power";
            this.changePowerCheckBox.UseVisualStyleBackColor = true;
            // 
            // costNumeric
            // 
            this.costNumeric.Location = new System.Drawing.Point(299, 38);
            this.costNumeric.Maximum = new decimal(new int[] {
            99999,
            0,
            0,
            0});
            this.costNumeric.Minimum = new decimal(new int[] {
            99999,
            0,
            0,
            -2147483648});
            this.costNumeric.Name = "costNumeric";
            this.costNumeric.Size = new System.Drawing.Size(101, 20);
            this.costNumeric.TabIndex = 25;
            // 
            // powerNumeric
            // 
            this.powerNumeric.Location = new System.Drawing.Point(414, 38);
            this.powerNumeric.Maximum = new decimal(new int[] {
            99999,
            0,
            0,
            0});
            this.powerNumeric.Minimum = new decimal(new int[] {
            99999,
            0,
            0,
            -2147483648});
            this.powerNumeric.Name = "powerNumeric";
            this.powerNumeric.Size = new System.Drawing.Size(101, 20);
            this.powerNumeric.TabIndex = 26;
            // 
            // CardViewSelectorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(542, 582);
            this.Controls.Add(this.powerNumeric);
            this.Controls.Add(this.costNumeric);
            this.Controls.Add(this.changePowerCheckBox);
            this.Controls.Add(this.changeCostCheckBox);
            this.Controls.Add(this.overrideFactionCheckBox);
            this.Controls.Add(this.factionBox);
            this.Controls.Add(this.showCatalogButton);
            this.Controls.Add(this.boardBox);
            this.Controls.Add(this.overrideBoardCheckBox);
            this.Controls.Add(this.overrideDescriptionCheckBox);
            this.Controls.Add(this.descriptionTextBox);
            this.Controls.Add(this.overrideCardBackCheckBox);
            this.Controls.Add(this.cardBackBox);
            this.Controls.Add(this.flipCardCheckBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.overrideCardCheckBox);
            this.Controls.Add(this.cardBox);
            this.Controls.Add(this.overrideBorderCheckBox);
            this.Controls.Add(this.borderBox);
            this.Controls.Add(this.force3DCheckbox);
            this.Controls.Add(this.ensureVariantMatchCheckbox);
            this.Controls.Add(this.overrideVariantCheckBox);
            this.Controls.Add(this.variantBox);
            this.Controls.Add(this.overrideRevealEffectCheckBox);
            this.Controls.Add(this.revealEffectBox);
            this.Controls.Add(this.overrideSurfaceEffectCheckBox);
            this.Controls.Add(this.surfaceEffectBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "CardViewSelectorForm";
            this.ShowIcon = false;
            this.Text = "Card view selector";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CardViewSelectorForm_FormClosing);
            this.Load += new System.EventHandler(this.CardViewSelectorForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.costNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.powerNumeric)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox surfaceEffectBox;
        private System.Windows.Forms.CheckBox overrideSurfaceEffectCheckBox;
        private System.Windows.Forms.CheckBox overrideRevealEffectCheckBox;
        private System.Windows.Forms.ComboBox revealEffectBox;
        private System.Windows.Forms.CheckBox overrideVariantCheckBox;
        private System.Windows.Forms.ComboBox variantBox;
        private System.Windows.Forms.CheckBox ensureVariantMatchCheckbox;
        private System.Windows.Forms.CheckBox force3DCheckbox;
        private System.Windows.Forms.CheckBox overrideBorderCheckBox;
        private System.Windows.Forms.ComboBox borderBox;
        private System.Windows.Forms.CheckBox overrideCardCheckBox;
        private System.Windows.Forms.ComboBox cardBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox flipCardCheckBox;
        private System.Windows.Forms.CheckBox overrideCardBackCheckBox;
        private System.Windows.Forms.ComboBox cardBackBox;
        private System.Windows.Forms.TextBox descriptionTextBox;
        private System.Windows.Forms.CheckBox overrideDescriptionCheckBox;
        private System.Windows.Forms.CheckBox overrideBoardCheckBox;
        private System.Windows.Forms.ComboBox boardBox;
        private System.Windows.Forms.Button showCatalogButton;
        private System.Windows.Forms.CheckBox overrideFactionCheckBox;
        private System.Windows.Forms.ComboBox factionBox;
        private System.Windows.Forms.CheckBox changeCostCheckBox;
        private System.Windows.Forms.CheckBox changePowerCheckBox;
        private System.Windows.Forms.NumericUpDown costNumeric;
        private System.Windows.Forms.NumericUpDown powerNumeric;
    }
}
