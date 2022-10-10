using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ImageAnalysis.Snapshot;
using Tools;
using ImageAnalysis.Сlustering;
using ImageProcessingTools.Extensions;
using OxyPlot;
using OxyPlot.Series;

namespace Interface
{
    public partial class Form1 : Form
    {
        private readonly Image defaultImage;
        private Image sourceImage;
        private Image bufferImage;

        public Form1()
        {
            InitializeComponent();
            DisableAllControlElements();
            defaultImage = sourcePictureBox.Image;
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            sourceImage = Image.FromFile(openFileDialog.FileName);
            sourcePictureBox.Image = sourceImage;
            processedPictureBox.Image = sourceImage;
            EnableAllControlElements();
            ShowBrightnessLevels();
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            processedPictureBox.Image.Save(saveFileDialog.FileName);
        }

        private void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            sourcePictureBox.Image = defaultImage;
            processedPictureBox.Image = defaultImage;
            DisableAllControlElements();
            plotView1.Model = null;
        }

        private void SourceButton_Click(object sender, EventArgs e)
        {
            processedPictureBox.Image = sourceImage;
            ShowBrightnessLevels();
        }

        private async void LinearContrastingButton_Click(object sender, EventArgs e)
        {
            var image = accumulativeProcessing.Checked ? processedPictureBox.Image : sourceImage;
            byte.TryParse(minBrightnessTextBox.Text, out byte min);
            byte.TryParse(maxBrightnessTextBox.Text, out byte max);
            processedPictureBox.Image = await image.LinearContrasting(min, max);
            ShowBrightnessLevels();
        }

        private async void MaxFilterButton_Click(object sender, EventArgs e)
        {
            var image = accumulativeProcessing.Checked ? processedPictureBox.Image : sourceImage;
            processedPictureBox.Image = await image.MaxFilterAsync();
            ShowBrightnessLevels();
        }

        private async void MinFilterButton_Click(object sender, EventArgs e)
        {
            var image = accumulativeProcessing.Checked ? processedPictureBox.Image : sourceImage;
            processedPictureBox.Image = await image.MinFilterAsync();
            ShowBrightnessLevels();
        }

        private async void MinMaxFilterButton_Click(object sender, EventArgs e)
        {
            var image = accumulativeProcessing.Checked ? processedPictureBox.Image : sourceImage;
            processedPictureBox.Image = await image.MinMaxFilterAsync();
            ShowBrightnessLevels();
        }

        private async void СlusteringButton_Click(object sender, EventArgs e)
        {
            var image = accumulativeProcessing.Checked ? processedPictureBox.Image : sourceImage;
            var snapshot = GetSnapshot(image);

            var vectors = new List<PropertyVector>();
            for (int areaNumber = snapshot.FirstAreaNumber; areaNumber <= snapshot.LastAreaNumber; ++areaNumber)
            {
                vectors.Add(CreatePropertyVector(snapshot, areaNumber));
            }

            byte.TryParse(classesNumberTextBox.Text, out byte classesNumber);
            ClusterSnapshot(snapshot, vectors, classesNumber);
            processedPictureBox.Image = await sourceImage.VisualizeClusterAsync(snapshot);
        }

        private ConnectedAreasSnapshot GetSnapshot(Image image)
        {
            var threshold = image.GetBinaryThreshold();
            var binarySnapshot = BinaryImageSnapshot.CreateFromImage(image, threshold);
            return ConnectedAreasSnapshot.Create(binarySnapshot);
        }

        private PropertyVector CreatePropertyVector(ConnectedAreasSnapshot snapshot, int areaNumber)
        {
            var perimeter = snapshot.GetPerimeter(areaNumber);
            var area = snapshot.GetArea(areaNumber);
            var massCenterX = snapshot.GetMassCenterX(areaNumber, area);
            var massCenterY = snapshot.GetMassCenterY(areaNumber, area);
            var centralMoment20 = snapshot.GetCentralMoment20(areaNumber, massCenterX, massCenterY);
            var centralMoment02 = snapshot.GetCentralMoment02(areaNumber, massCenterX, massCenterY);
            var centralMoment11 = snapshot.GetCentralMoment11(areaNumber, massCenterX, massCenterY);

            var elongation = PropertyCalculator.GetElongation(centralMoment20, centralMoment02, centralMoment11);
            var compactness = PropertyCalculator.GetCompactness(area, perimeter);
            return new PropertyVector(compactness, elongation, areaNumber);
        }

        private void ClusterSnapshot(ConnectedAreasSnapshot snapshot, List<PropertyVector> vectors, int numberOfClusters)
        {
            var clusteredVectors = Сlusterer.MakeClusters(vectors, numberOfClusters);
            for (int row = 0; row < snapshot.Height; ++row)
            {
                for (int column = 0; column < snapshot.Width; ++column)
                {
                    if (snapshot[row, column] >= snapshot.FirstAreaNumber)
                    {
                        snapshot[row, column] = clusteredVectors.First(v => v.AreaNumber == snapshot[row, column]).ClassNumber;
                    }
                }
            }
        }

        private void DisableAllControlElements()
        {
            sourceButton.Enabled = false;
            minFilterButton.Enabled = false;
            maxFilterButton.Enabled = false;
            minMaxFilterButton.Enabled = false;
            minBrightnessTextBox.Enabled = false;
            maxBrightnessTextBox.Enabled = false;
            linearContrastingButton.Enabled = false;
            saveToolStripMenuItem.Enabled = false;
            closeToolStripMenuItem.Enabled = false;
            clusteringButton.Enabled = false;
            checkBinarizationButton.Enabled = false;
            classesNumberTextBox.Enabled = false;
            accumulativeProcessing.Enabled = false;
            showRgbCheckBox.Enabled = false;
        }

        private void EnableAllControlElements()
        {
            sourceButton.Enabled = true;
            minFilterButton.Enabled = true;
            maxFilterButton.Enabled = true;
            minMaxFilterButton.Enabled = true;
            minBrightnessTextBox.Enabled = true;
            maxBrightnessTextBox.Enabled = true;
            linearContrastingButton.Enabled = true;
            saveToolStripMenuItem.Enabled = true;
            closeToolStripMenuItem.Enabled = true;
            clusteringButton.Enabled = true;
            checkBinarizationButton.Enabled = true;
            classesNumberTextBox.Enabled = true;
            accumulativeProcessing.Enabled = true;
            showRgbCheckBox.Enabled = true;
        }

        private void ClassesNumberTextBox_Leave(object sender, EventArgs e)
        {
            var value = classesNumberTextBox.Text;
            if (value.Any(c => !char.IsDigit(c)))
            {
                classesNumberTextBox.Text = "2";
            }
            else if (!int.TryParse(value, out int number) || number < 2)
            {
                classesNumberTextBox.Text = "2";
            }
        }

        private void MinBrightnessTextBox_Leave(object sender, EventArgs e)
        {
            var value = minBrightnessTextBox.Text;
            if (value.Any(c => !char.IsDigit(c)))
            {
                minBrightnessTextBox.Text = "0";
            }
            else if (!int.TryParse(value, out int min) || min > 255)
            {
                minBrightnessTextBox.Text = "0";
            }
        }

        private void MaxBrightnessTextBox_Leave(object sender, EventArgs e)
        {
            var value = maxBrightnessTextBox.Text;
            if (value.Any(c => !char.IsDigit(c)))
            {
                maxBrightnessTextBox.Text = "255";
            }
            else if (!int.TryParse(value, out int max) || max > 255)
            {
                maxBrightnessTextBox.Text = "255";
            }
        }

        private void ShowBrightnessLevels()
        {
            var plotModel = new PlotModel
            {
                Title = "Brightness levels",
                PlotType = PlotType.XY,
            };

            var whiteColor = OxyColor.FromRgb(255, 255, 255);
            plotModel.TextColor = whiteColor;
            plotModel.TitleColor = whiteColor;
            plotModel.PlotAreaBorderColor = whiteColor;

            if (showRgbCheckBox.Checked)
            {
                var redLevels = processedPictureBox.Image.GetBrightnessLevels(2);
                var greenLevels = processedPictureBox.Image.GetBrightnessLevels(1);
                var blueLevels = processedPictureBox.Image.GetBrightnessLevels(0);

                plotModel.Series.Add(GetBrightnessSeries(redLevels, OxyColor.FromRgb(200, 0, 0)));
                plotModel.Series.Add(GetBrightnessSeries(greenLevels, OxyColor.FromRgb(0, 200, 0)));
                plotModel.Series.Add(GetBrightnessSeries(blueLevels, OxyColor.FromRgb(0, 0, 200)));
            }
            else
            {
                var grayLevels = processedPictureBox.Image.GetBrightnessLevels();
                plotModel.Series.Add(GetBrightnessSeries(grayLevels, OxyColor.FromRgb(255, 255, 255)));
            }

            plotView1.Model = plotModel;
        }

        private Series GetBrightnessSeries(List<int> brightnessLevels, OxyColor seriesColor)
            => new FunctionSeries((x) => brightnessLevels[(int)x], 0, brightnessLevels.Count, 0.1)
                {
                    Color = seriesColor
                };

        private void ShowRgbCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            ShowBrightnessLevels();
        }

        private async void CheckBinarizationButton_MouseDown(object sender, MouseEventArgs e)
        {
            bufferImage = processedPictureBox.Image;
            processedPictureBox.Image = await bufferImage.BinorizeAsync();
        }

        private void CheckBinarizationButton_MouseUp(object sender, MouseEventArgs e)
        {
            processedPictureBox.Image = bufferImage;
        }
    }
}