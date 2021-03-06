// Copyright 2021 AliveDevil (https://github.com/AliveDevil/pdf-image-replace)

using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.IO;

new CommandLineBuilder()
    .UseHelp()
    .AddGlobalOption(new Option<FileInfo>("--file").WithAlias("-f").LegalFilePathsOnly())
    .AddCommand(new CommandBuilder(new Command("export"))
        .AddOption(new Option<DirectoryInfo>("--output").WithAlias("-o").LegalFilePathsOnly())
        .WithHandler(CommandHandler.Create((FileInfo file, DirectoryInfo output) =>
        {
            using var reader = new PdfReader(file);
            using var document = new PdfDocument(reader);
            for (int i = document.GetNumberOfPages(); i > 0; i--)
            {
                var targetDirectory = output.CreateSubdirectory(i.ToString());
                var page = document.GetPage(i);
                var resources = page.GetResources();
                var xobjs = resources.GetResource(PdfName.XObject);
                if (xobjs is null) { continue; }
                foreach (var item in xobjs.KeySet())
                {
                    var obj = xobjs.Get(item);
                    if (!obj.IsIndirect() || obj is not PdfStream stream)
                    {
                        continue;
                    }
                    if (stream.Get(PdfName.Type) != PdfName.XObject || stream.Get(PdfName.Subtype) != PdfName.Image)
                    {
                        continue;
                    }
                    var name = stream.GetAsName(PdfName.Name).GetValue();
                    if (PdfXObject.MakeXObject(stream) is not PdfImageXObject pdfImage)
                    {
                        continue;
                    }
                    var extension = pdfImage.IdentifyImageFileExtension();
                    var type = pdfImage.IdentifyImageType();
                    File.WriteAllBytes(Path.ChangeExtension(Path.Combine(targetDirectory.FullName, name), extension), pdfImage.GetImageBytes());
                }
            }
        })).Command)
    .AddCommand(new CommandBuilder(new Command("update"))
        .AddOption(new Option<int>("--page")
        {
            IsRequired = true
        })
        .AddOption(new Option<int?>("--index"))
        .AddOption(new Option<string>("--name"))
        .AddOption(new Option<FileInfo>("--image")
        {
            IsRequired = true
        })
        .WithHandler(CommandHandler.Create((FileInfo file, int page, int? index, string name, FileInfo image) =>
        {
            if (index is null && string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Neither Index nor Name cannot be unset");
            }
            if (index is not null && !string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Neither Index nor Name can both be set at the same time.");
            }
            using var reader = new PdfReader(file);
            using var writer = new PdfWriter(Path.ChangeExtension(Path.Combine(
                file.DirectoryName,
                Path.GetFileNameWithoutExtension(file.Name) + "-updated"), file.Extension));
            using var document = new PdfDocument(reader, writer);
            var documentPage = document.GetPage(page);
            var resources = documentPage.GetResources();
            var xobjs = resources.GetResource(PdfName.XObject);
            var testIndex = 0;
            PdfName setName = default;
            foreach (var item in xobjs.KeySet())
            {
                var obj = xobjs.Get(item);
                if (!obj.IsIndirect() || obj is not PdfStream stream)
                {
                    continue;
                }
                if (stream.Get(PdfName.Type) != PdfName.XObject || stream.Get(PdfName.Subtype) != PdfName.Image)
                {
                    continue;
                }
                var objName = stream.GetAsName(PdfName.Name).GetValue();
                if (PdfXObject.MakeXObject(stream) is not PdfImageXObject pdfImage)
                {
                    continue;
                }
                testIndex += 1;
                if (testIndex == index || string.Equals(objName, name, StringComparison.OrdinalIgnoreCase))
                {
                    setName = item;
                    break;
                }
            }
            if (setName is null)
            {
                throw new KeyNotFoundException();
            }
            var data = ImageDataFactory.Create(image.FullName);
            var imageObject = new PdfImageXObject(data);
            xobjs.Put(setName, imageObject.GetPdfObject());
        })).Command)
    .Command.Invoke(Environment.CommandLine);

internal static class Extensions
{
    public static T WithAlias<T>(this T option, string alias) where T : Option
    {
        option.AddAlias(alias);
        return option;
    }

    public static Command WithHandler(this Command command, ICommandHandler handler)
    {
        command.Handler = handler;
        return command;
    }

    public static CommandBuilder WithHandler(this CommandBuilder command, ICommandHandler handler)
    {
        command.Command.Handler = handler;
        return command;
    }
}