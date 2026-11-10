using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using UBS_XML_protocol_demo.Properties;
using System.Runtime;
using System.Xml;
using ClosedXML.Excel;
using System.Data;


namespace UBS_XML_protocol_demo
{
    public partial class Form1 : Form
    {

        public class AppSettings
        {
            public string IP { get; set; }
            public int Port { get; set; } = 5900;
            public bool Board_Id { get; set; } = true;
            public bool PH_Index { get; set; } = false;
            public bool PH_Name { get; set; } = false;
            public string Board_Id_text { get; set; } = "0";
            public string PH_Index_text { get; set; } = "1";
            public string PH_Name_text { get; set; } = "PH#0";




        }

        private TcpClient client;
        private NetworkStream stream;
        private string printerID;
        private int pars_flag = 0;

        private string settingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "settings.json"
        );





        public Form1()
        {
            InitializeComponent();
            comboBox1.SelectedIndex = 0;
            LoadSettings();

            button1.Text = "Connect";

        }

        private void LoadSettings()
        {
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);

                AppSettings settings =
                    JsonConvert.DeserializeObject<AppSettings>(json);

                textBox1.Text = settings.IP;
                textBox2.Text = settings.Port.ToString();

                checkBox1.Checked = settings.Board_Id;
                checkBox2.Checked = settings.PH_Index;
                checkBox3.Checked = settings.PH_Name;

                textBox3.Text = settings.Board_Id_text;
                textBox4.Text = settings.PH_Index_text;
                textBox5.Text = settings.PH_Name_text;

            }
            else
            {
                // Настройки по умолчанию
                textBox1.Text = "";
                textBox2.Text = "9900";

                checkBox1.Checked = true;
                checkBox2.Checked = false;
                checkBox3.Checked = false;

                textBox3.Text = "0";
                textBox4.Text = "1";
                textBox5.Text = "PH#0";


                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            AppSettings settings = new AppSettings
            {
                IP = textBox1.Text,
                Port = int.Parse(textBox2.Text),

                Board_Id = checkBox1.Checked,
                PH_Index = checkBox2.Checked,
                PH_Name = checkBox3.Checked,

                Board_Id_text = textBox3.Text,
                PH_Index_text = textBox4.Text,
                PH_Name_text = textBox5.Text
            };

            string json = JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);

            File.WriteAllText(settingsPath, json);

        }




        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            Disconnect();

            base.OnFormClosing(e);
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (client == null)
            {
                // CONNECT
                int ip_port = int.Parse(textBox2.Text);
                await ConnectAsync(textBox1.Text, ip_port);
            }
            else
            {
                // DISCONNECT
                Disconnect();
            }
        }




        private async Task ConnectAsync(string ip, int port)
        {
            try
            {
                client = new TcpClient();

                await client.ConnectAsync(ip, port);

                stream = client.GetStream();

                button1.Text = "Disconnect";

                richTextBox1.AppendText("Connected!" + Environment.NewLine);

                // Запускаем приём данных
                _ = ReceiveDataAsync();
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText("Connection error: " + ex.Message + Environment.NewLine);

                Disconnect();
            }

        }

        private void Disconnect()
        {
            stream?.Close();
            client?.Close();

            stream = null;
            client = null;

            button1.Text = "Connect";

            richTextBox1.AppendText("Disconnected!" + Environment.NewLine);
        }
        private async void button2_Click(object sender, EventArgs e)
        {

            if (comboBox1.SelectedItem.ToString() == "GET_COUNTER_LIST" ||
                comboBox1.SelectedItem.ToString() == "GET_VARIABLE_LIST")
            {


                if (comboBox2.SelectedItem != null)
                {
                    await SendPrinterCommandAsync(comboBox1.SelectedItem.ToString(), "Format=\"" + comboBox2.SelectedItem.ToString() + "\"");
                }    

                 else{
                    MessageBox.Show("Format not selected!");
                    return;
                }
                
            }
            
            else await SendPrinterCommandAsync(comboBox1.SelectedItem.ToString());
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            if (comboBox2.SelectedItem != null)
            {
                pars_flag = 2;
                await SendPrinterCommandAsync("GET_VARIABLE_LIST", "Format=\"" + comboBox2.SelectedItem.ToString() + "\"");
            }

            else
            {
                MessageBox.Show("Format not selected!");
                return;
            }

        }
        private async Task SendPrinterCommandAsync(string send_command, string arg1 = "")
        {
            if (stream == null)
                return;

            if (checkBox1.Checked) printerID = "BoardId = \"" + textBox3.Text + "\"";
            else if (checkBox2.Checked) printerID = "Printhead = \"" + textBox4.Text + "\"";
            else if (checkBox3.Checked) printerID = "PhName = \"" + textBox5.Text + "\"";



            string command =
                "<PROTOCOL>\r\n" +
                "<" + send_command + " " + arg1 + " " + printerID + "/>\r\n" +

                "</PROTOCOL>";

            // Преобразуем текст команды в байты UTF-8
            byte[] text = Encoding.UTF8.GetBytes(command);

            // Формируем полный пакет
            byte[] packet = new byte[text.Length + 8];

            // Start markers
            packet[0] = 0x27;
            packet[1] = 0x01;
            packet[2] = 0x27;
            packet[3] = 0x02;

            // Command text
            Array.Copy(text, 0, packet, 4, text.Length);

            // End markers
            packet[packet.Length - 4] = 0x27;
            packet[packet.Length - 3] = 0x03;
            packet[packet.Length - 2] = 0x27;
            packet[packet.Length - 1] = 0x04;

            try
            {
                // Отправка TCP-пакета
                await stream.WriteAsync(packet, 0, packet.Length);

                // Отображение отправленной команды
                richTextBox1.SelectionStart = richTextBox1.TextLength;
                richTextBox1.SelectionColor = Color.Blue;

                richTextBox1.AppendText(
                    "TX:\r\n 0x27 0x01 0x27 0x02" +
                    command +
                    "0x27 0x03 0x27 0x04\r\n"
                );

                // Разделитель
                richTextBox1.SelectionColor = Color.Gray;
                richTextBox1.AppendText("--------------------------------\r\n");

                // Возвращаем стандартный цвет
                richTextBox1.SelectionColor = Color.Black;

                richTextBox1.SelectionStart = richTextBox1.TextLength;
                richTextBox1.ScrollToCaret();
            }
            catch (Exception ex)
            {
                richTextBox1.SelectionStart = richTextBox1.TextLength;
                richTextBox1.SelectionColor = Color.Red;

                richTextBox1.AppendText(
                    "TX ERROR: " + ex.Message + Environment.NewLine
                );

                richTextBox1.SelectionColor = Color.Black;
            }
        }

        private async Task ReceiveDataAsync()
        {
            byte[] buffer = new byte[4096];

            try
            {
                while (client != null && client.Connected)
                {
                    int count = await stream.ReadAsync(buffer, 0, buffer.Length);

                    // Сервер закрыл соединение
                    if (count == 0)
                        break;

                    string data = Encoding.UTF8.GetString(buffer, 0, count);

                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            richTextBox1.AppendText("RX:\r\n" + data + Environment.NewLine);
                            richTextBox1.AppendText("--------------------------------" + Environment.NewLine);

                            richTextBox1.SelectionStart = richTextBox1.TextLength;
                            richTextBox1.ScrollToCaret();
                        }));
                    }
                    if (pars_flag == 1) LoadFormatList(data);
                    if (pars_flag == 2) LoadVariableList(data);
                    pars_flag = 0;
                }
            }
            catch (ObjectDisposedException)
            {
                // Поток закрыт при отключении
            }
            catch (Exception ex)
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        richTextBox1.AppendText("RX ERROR: " + ex.Message + Environment.NewLine);
                    }));
                }
            }
            finally
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        richTextBox1.AppendText("Connection closed." + Environment.NewLine);
                        //Disconnect();
                    }));
                }
            }


        }

        private void LoadFormatList(string response)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(response);

            comboBox2.Items.Clear();

            XmlNodeList formats = doc.SelectNodes("//FORMAT");

            foreach (XmlNode format in formats)
            {
                string name = format.Attributes["Value"].Value;
                comboBox2.Items.Add(name);
            }

            if (comboBox2.Items.Count > 0)
                comboBox2.SelectedIndex = 0;
        }

        private void LoadVariableList(string response)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(response);

            comboBox3.Items.Clear();
            comboBox6.Items.Clear();
            comboBox8.Items.Clear();

            XmlNodeList variables = doc.SelectNodes("//VARIABLE");

            foreach (XmlNode variable in variables)
            {
                string name = variable.Attributes["Name"].Value;

                comboBox3.Items.Add(name);
                comboBox6.Items.Add(name);
                comboBox8.Items.Add(name);
            }

            if (comboBox3.Items.Count > 0)
                comboBox3.SelectedIndex = 0;

            if (comboBox6.Items.Count > 0)
                comboBox6.SelectedIndex = 0;

            if (comboBox8.Items.Count > 0)
                comboBox8.SelectedIndex = 0;
        }


        private void Form1_Load(object sender, EventArgs e)
        {


        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                checkBox2.Checked = false;
                checkBox3.Checked = false;
                textBox3.Enabled = true;
                textBox4.Enabled = false;
                textBox5.Enabled = false;

            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                checkBox1.Checked = false;
                checkBox3.Checked = false;
                textBox3.Enabled = false;
                textBox4.Enabled = true;
                textBox5.Enabled = false;
            }
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked)
            {
                checkBox1.Checked = false;
                checkBox2.Checked = false;
                textBox3.Enabled = false;
                textBox4.Enabled = false;
                textBox5.Enabled = true;
            }
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            pars_flag = 1;
            await SendPrinterCommandAsync("GET_FORMAT_LIST");
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Excel files (*.xlsx)|*.xlsx";
                dialog.Title = "Open Excel file";

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                textBox6.Text = dialog.FileName;

                try
                {
                    using (XLWorkbook workbook = new XLWorkbook(dialog.FileName))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var range = worksheet.RangeUsed();

                        if (range == null)
                            return;

                        int firstRow = range.RangeAddress.FirstAddress.RowNumber;
                        int lastRow = range.RangeAddress.LastAddress.RowNumber;

                        int firstCol = range.RangeAddress.FirstAddress.ColumnNumber;
                        int lastCol = range.RangeAddress.LastAddress.ColumnNumber;

                        DataTable table = new DataTable();

                        // Первая строка Excel — заголовки столбцов
                        for (int col = firstCol; col <= lastCol; col++)
                        {
                            string header = worksheet.Cell(firstRow, col).GetString();

                            if (string.IsNullOrWhiteSpace(header))
                                header = "Column" + col;

                            string uniqueHeader = header;
                            int suffix = 1;

                            while (table.Columns.Contains(uniqueHeader))
                            {
                                uniqueHeader = header + "_" + suffix;
                                suffix++;
                            }

                            table.Columns.Add(uniqueHeader);
                        }

                        // Читаем остальные строки
                        for (int row = firstRow + 1; row <= lastRow; row++)
                        {
                            DataRow dataRow = table.NewRow();

                            for (int col = firstCol; col <= lastCol; col++)
                            {
                                dataRow[col - firstCol] =
                                    worksheet.Cell(row, col).GetFormattedString();
                            }

                            table.Rows.Add(dataRow);
                        }

                        dataGridView1.DataSource = table;

                        dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                        // Начальная ширина всех столбцов
                        foreach (DataGridViewColumn column in dataGridView1.Columns)
                        {
                            column.Width = 150;
                        }

                        // Перенос текста
                        dataGridView1.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                        // Автоматическая высота строк
                        dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

                        // Разрешить изменение ширины мышкой
                        dataGridView1.AllowUserToResizeColumns = true;


                        dataGridView1.RowHeadersWidth = 10;
                        dataGridView1.RowHeadersWidthSizeMode =
                        DataGridViewRowHeadersWidthSizeMode.DisableResizing;


                        // Выделять всю строку целиком
                        dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

                        // Запретить выделение отдельных ячеек
                        dataGridView1.MultiSelect = false;




                        comboBox4.Items.Clear();
                        comboBox5.Items.Clear();
                        comboBox7.Items.Clear();

                        foreach (DataGridViewColumn column in dataGridView1.Columns)
                        {
                            comboBox4.Items.Add(column.Name);
                            comboBox5.Items.Add(column.Name);
                            comboBox7.Items.Add(column.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка чтения Excel: " + ex.Message);
                }
            }
        }

        private async void button6_Click(object sender, EventArgs e)
        {
            // Проверяем, что выбрана строка Excel
            if (checkBox1.Checked) printerID = "BoardId = \"" + textBox3.Text + "\"";
            else if (checkBox2.Checked) printerID = "Printhead = \"" + textBox4.Text + "\"";
            else if (checkBox3.Checked) printerID = "PhName = \"" + textBox5.Text + "\"";


            if (dataGridView1.CurrentRow == null)
            {
                MessageBox.Show("Выбери строку Excel!");
                return;
            }

            // Проверяем выбранный формат и переменные
            if (comboBox2.SelectedItem == null ||
                comboBox3.SelectedItem == null ||
                comboBox5.SelectedItem == null ||
                comboBox7.SelectedItem == null ||
                comboBox4.SelectedItem == null ||
                comboBox6.SelectedItem == null ||
                comboBox8.SelectedItem == null)
            {
                MessageBox.Show("Не все параметры выбраны!");
                return;
            }

            // Получаем выбранный формат печати
            string format = comboBox2.Text;

            // Получаем имена переменных принтера
            string var1 = comboBox3.Text;
            string var2 = comboBox6.Text;
            string var3 = comboBox8.Text;

            // Получаем выбранную строку Excel
            DataGridViewRow row = dataGridView1.CurrentRow;

            // Получаем значения из соответствующих столбцов
            string value1 = Convert.ToString(row.Cells[comboBox4.Text].Value);
            string value2 = Convert.ToString(row.Cells[comboBox5.Text].Value);
            string value3 = Convert.ToString(row.Cells[comboBox7.Text].Value);

            // Экранируем специальные символы XML
            format = System.Security.SecurityElement.Escape(format);

            var1 = System.Security.SecurityElement.Escape(var1);
            var2 = System.Security.SecurityElement.Escape(var2);
            var3 = System.Security.SecurityElement.Escape(var3);

            value1 = System.Security.SecurityElement.Escape(value1);
            value2 = System.Security.SecurityElement.Escape(value2);
            value3 = System.Security.SecurityElement.Escape(value3);

            // Формируем XML-команду
            string command =
                "<SET_PRINTING_FORMAT Format=\"" + format +
                "\" Quantity=\"0\" useCache=\"false\" " + printerID + ">\r\n" +
                //"<" + send_command + " " + arg1 + " " + printerID + "/>\r\n" +

                "<VARIABLE Name=\"" + var1 + "\" Value=\"" + value1 + "\" />\r\n" +

                "<VARIABLE Name=\"" + var2 + "\" Value=\"" + value2 + "\" />\r\n" +

                "<VARIABLE Name=\"" + var3 + "\" Value=\"" + value3 + "\" />\r\n" +

                "</SET_PRINTING_FORMAT>";

            // Отправляем команду принтеру
            await SendPrinterXmlAsync(command);
        }

        private async Task SendPrinterXmlAsync(string command)
        {
            if (stream == null)
            {
                MessageBox.Show("Нет подключения к принтеру!");
                return;
            }

            string xml =
                "<PROTOCOL>\r\n" +
                command +
                "\r\n</PROTOCOL>";

            byte[] text = Encoding.UTF8.GetBytes(xml);
            byte[] packet = new byte[text.Length + 8];

            // Начало пакета
            packet[0] = 0x27;
            packet[1] = 0x01;
            packet[2] = 0x27;
            packet[3] = 0x02;

            Array.Copy(text, 0, packet, 4, text.Length);

            // Конец пакета
            packet[packet.Length - 4] = 0x27;
            packet[packet.Length - 3] = 0x03;
            packet[packet.Length - 2] = 0x27;
            packet[packet.Length - 1] = 0x04;

            richTextBox1.SelectionStart = richTextBox1.TextLength;
            richTextBox1.SelectionColor = Color.Blue;
            richTextBox1.AppendText("TX:\r\n 0x27 0x01 0x27 0x02" + xml+ "0x27 0x03 0x27 0x04" + Environment.NewLine);
            

            richTextBox1.SelectionColor = Color.Gray;
            richTextBox1.AppendText("--------------------------------" + Environment.NewLine);

            richTextBox1.SelectionColor = Color.Black;
            richTextBox1.SelectionStart = richTextBox1.TextLength;
            richTextBox1.ScrollToCaret();

            //Отправляем пакет
            await stream.WriteAsync(packet, 0, packet.Length);
            // Отображаем отправляемый пакет в терминале
            //richTextBox1.SelectionStart = richTextBox1.TextLength;
            //richTextBox1.SelectionColor = Color.Blue;

            //richTextBox1.AppendText("0x27 0x01 0x27 0x02");

            //// Сам XML обычным текстом
            //richTextBox1.AppendText(xml);

            //richTextBox1.AppendText("0x27 0x03 0x27 0x04");
            //richTextBox1.AppendText(Environment.NewLine);

            //richTextBox1.SelectionColor = Color.Gray;
            //richTextBox1.AppendText("--------------------------------" + Environment.NewLine);

            //richTextBox1.SelectionColor = Color.Black;
            //richTextBox1.SelectionStart = richTextBox1.TextLength;
            //richTextBox1.ScrollToCaret();

            //// Отправляем пакет
            //await stream.WriteAsync(packet, 0, packet.Length);
        }
    }

}
