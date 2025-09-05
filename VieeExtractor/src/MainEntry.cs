using System.Drawing;
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
    private IExtractor? _extractor;
    private readonly Label _choicesLabel;

    public MainEntry() {
        ClientSize = new Size(480, 320);
        SuspendLayout();
        _label = new Label { AutoSize = true, Text = "Text:" };
        _choicesLabel = new Label { AutoSize = true, Text = "Choices:" };
        _choicesLabel.Top = 160;
        Controls.Add(_label);
        Controls.Add(_choicesLabel);
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
    }

    protected override void FastUpdateAfter()
    {
        base.FastUpdateAfter();
        _extractor?.FrameEnd(true);
    }

    public void OnNewText(string text)
    {
        _label.Text = "Text:\n" + text;
        var d = ExtractorData.CreateText(text);
        _tcpServer.SendMessage(JsonSerializer.Serialize(d));
    }

    public void OnNewChoices(string[] choices, int index)
    {
        _choicesLabel.Text = "Choices:\n" + string.Join(",", choices);
        var d = ExtractorData.CreateChoices(choices, index);
        _tcpServer.SendMessage(JsonSerializer.Serialize(d));

    }
}