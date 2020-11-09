using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace lab5
{
    public partial class FormImageScraper : Form
    {
        private HttpClient client = new HttpClient();
        private Stopwatch stopwatch = new Stopwatch();
        private Dictionary<Task<byte[]>, string> allImageBytes = new Dictionary<Task<byte[]>, string>();
       
        private int totalDownloaded;
        private int failedDownload;

        public FormImageScraper()
        {
            InitializeComponent();
        }

        private async void buttonExtract_Click(object sender, EventArgs e)
        {
            listBoxMatches.Items.Clear();

            string pattern = @"(?<=<img.*src="")[^""]+(?="".*)";

            string inputURI = textBoxURI.Text;

            if (!textBoxURI.Text.StartsWith("http"))
            {
                inputURI = "http://" + textBoxURI.Text;
            } 

            string website = await client.GetStringAsync(inputURI);

            MatchCollection matches = Regex.Matches(website, pattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                if (!match.Value.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                {
                    listBoxMatches.Items.Add(inputURI + match.Value);
                }
                else
                {
                    listBoxMatches.Items.Add(match.Value);
                }
            }

            if (listBoxMatches.Items.Count != 0)
            {
                buttonSave.Enabled = true;
                progressBar.Maximum = listBoxMatches.Items.Count;
                labelImagesFound.Text = $"Images found: {listBoxMatches.Items.Count}";
            }
        }
        private async void buttonSave_Click(object sender, EventArgs e)
        {
            stopwatch.Restart();
            progressBar.Value = 0;
            totalDownloaded = 0;
            failedDownload = 0;

            if (listBoxMatches.Items.Count == 0)
            {
                MessageBox.Show(this, "Could not find images.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }
            else if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            string directory = folderBrowserDialog.SelectedPath;

            Directory.CreateDirectory(directory);

            foreach (string URL in listBoxMatches.Items)
            {
                Task<byte[]> imageTask = client.GetByteArrayAsync(URL);

                allImageBytes.Add(imageTask, URL);
            }

            await ProcessTasks(directory);
        }

        private async Task ProcessTasks(string directory)
        {
            while (allImageBytes.Count > 0)
            {
                Task<byte[]> completedTask = null;
                    
                try
                {
                    completedTask = await Task.WhenAny(allImageBytes.Keys);

                    Uri fileURL = new Uri(allImageBytes[completedTask]);    

                    using (FileStream fileStream = File.Open(Path.Combine(directory, totalDownloaded + Path.GetFileName(fileURL.AbsolutePath)), FileMode.Create))
                    {
                        await fileStream.WriteAsync(completedTask.Result, 0, completedTask.Result.Length);

                        if (completedTask.Status == TaskStatus.RanToCompletion)
                        {
                            totalDownloaded++;
                        }
                        else
                        {
                            failedDownload++;
                        }

                        IncrementProgressBar();
                    }

                    allImageBytes.Remove(completedTask);
                }
                catch (Exception e)
                {
                    
                }
                finally
                {
                    if (completedTask != null)
                    {
                        allImageBytes.Remove(completedTask);
                    }
                }
            }
        }
        private void IncrementProgressBar()
        {
            progressBar.Value++;

            if (progressBar.Value == listBoxMatches.Items.Count)
            {
                if (failedDownload > 0)
                {
                    MessageBox.Show(this, $"{totalDownloaded} images downloaded\n" +
                        $"{failedDownload} images failed to download." +
                    $"Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, $"{totalDownloaded} images downloaded\n" +
                    $"Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                
                buttonSave.Visible = true;
                stopwatch.Stop();
            }
        }
    }
}
