using System;
using System.Drawing;
using System.Windows.Forms;
using BizHawk.Client.EmuHawk;
using BizHawk.Emulation.Common;
using VieeExtractor.Server;

namespace VieeExtractor;

using BizHawk.Client.Common;

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
        _label = new Label { AutoSize = true, Text = "VieeExtractor" };
        _choicesLabel = new Label { AutoSize = true, Text = "Choices" };
        _choicesLabel.Top = 160;
        Controls.Add(_label);
        Controls.Add(_choicesLabel);
        ResumeLayout(performLayout: false);
        PerformLayout();
        _tcpServer = new TcpServer(42184);
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
        _label.Text = text;
        _tcpServer.SendMessage(text);
    }

    public void OnNewChoices(string[] choices, int index)
    {
        var joined = string.Join(",", choices);
        _choicesLabel.Text = joined;
    }
}
