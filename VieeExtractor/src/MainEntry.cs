using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk;
using BizHawk.Emulation.Common;
using VieeExtractor.Server;

namespace VieeExtractor;

[ExternalTool("VieeExtractor")] // this appears in the Tools > External Tools submenu in EmuHawk
[ExternalToolApplicability.RomList(VSystemID.Raw.PSX, "37946519", "9A49C0E4")]
public sealed class MainEntry : ToolFormBase, IExternalToolForm, IExtractResultListener
{
    protected override string WindowTitleStatic => "VieeExtractor";
    
    [OptionalApi]
    private IEmulationApi? MaybeEmulationApi { get; set; }

    public ApiContainer? MaybeApiContainer { get; set; }
    
    private ApiContainer APIs => MaybeApiContainer!;

    private readonly Label _label;
    private readonly TcpServer _tcpServer;
    private readonly Label _choicesLabel;
    private readonly CheckBox _check;
    private readonly Label _statusLabel;
    private IExtractor? _extractor;
    private int _memoizedClientCount = -1;

    public MainEntry() {
        ClientSize = new Size(480, 320);
        SuspendLayout();
        _label = new Label { AutoSize = true, Text = "Text:" };
        _label.Top = 24;
        _choicesLabel = new Label { AutoSize = true, Text = "Choices:" };
        _choicesLabel.Top = 160;
        _statusLabel = new Label { AutoSize = true, Text = "" };
        _statusLabel.Top = 320 - 16;
        _check = new CheckBox { AutoSize = true, Text = "Pause extraction in turbo mode" };
        Controls.Add(_label);
        Controls.Add(_choicesLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_check);
        ResumeLayout(performLayout: false);
        PerformLayout();
        _tcpServer = new TcpServer(42184);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _tcpServer.Dispose();
    }

    public override void Restart()
    {
        base.Restart();
        var gameInfo = MaybeEmulationApi?.GetGameInfo();
        _extractor = ExtractorFactory.Create(gameInfo?.Hash, APIs, this);
    }

    protected override void UpdateAfter()
    {
        base.UpdateAfter();
        _extractor?.FrameEnd(false);
        UpdateConnectedClientCount();
    }

    private void UpdateConnectedClientCount()
    {
        var count = _tcpServer.ClientCount;
        if (_memoizedClientCount != count)
        {
            _memoizedClientCount = count;
            _statusLabel.Text = $"Connected Client: {count}";
        }
    }

    protected override void FastUpdateAfter()
    {
        base.FastUpdateAfter();
        _extractor?.FrameEnd(_check.Checked);
        UpdateConnectedClientCount();
    }
    public void OnNewData(ExtractorData data)
    {
        switch (data.Type)
        {
            case ExtractorData.TypeText:
                if (string.IsNullOrEmpty(data.Speaker))
                {
                    _label.Text = data.Text;
                }
                else
                {
                    _label.Text = $"{data.Speaker}：\n{data.Text}";
                }
                break;
            case ExtractorData.TypeChoice:
                var choicesWithSelect = data.Choices.Select((v, i) => i == data.Index ? $"> {v}" : v);
                _choicesLabel.Text = string.Join(", ", choicesWithSelect);
                break;
        }
        _tcpServer.SendMessage(JsonSerializer.Serialize(data));
    }
}