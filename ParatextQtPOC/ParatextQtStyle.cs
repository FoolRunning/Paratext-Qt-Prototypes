using System;
using System.IO;
using QtCore;
using QtCore.Qt;
using QtGui;
using QtWidgets;

namespace ParatextQtPOC
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Ported from https://doc.qt.io/qt-6/qtwidgets-widgets-styles-example.html </remarks>
    internal class ParatextQtStyle : QProxyStyle
    {
        private QPalette m_standardPalette;

        public ParatextQtStyle() : base(QStyleFactory.Create("Windows"))
        {
            ObjectName = "NorwegianWood";
        }

        public override QPalette StandardPalette
        {
            get
            {
                if (m_standardPalette == null)
                {
                    QColor brown = new(212, 140, 95);
                    QColor beige = new(236, 182, 120);
                    QColor slightlyOpaqueBlack = new(0, 0, 0, 63);

                    QImage backgroundImage = new(Path.Combine(Environment.CurrentDirectory, "resources", "woodbackground.png"));
                    QImage buttonImage = new(Path.Combine(Environment.CurrentDirectory, "resources", "woodbutton.png"));
                    QImage midImage = buttonImage.ConvertToFormat(QImage.Format.FormatRGB32);

                    using (QPainter painter = new QPainter())
                    {
                        painter.Begin(midImage);
                        painter.SetPen(PenStyle.NoPen);
                        painter.FillRect(midImage.Rect, slightlyOpaqueBlack);
                        painter.End();
                    }

                    QPalette palette = new QPalette(brown);

                    palette.SetBrush(QPalette.ColorRole.BrightText, GlobalColor.White);
                    palette.SetBrush(QPalette.ColorRole.Base, beige);
                    palette.SetBrush(QPalette.ColorRole.Highlight, GlobalColor.DarkGreen);
                    SetTexture(palette, QPalette.ColorRole.Button, buttonImage);
                    SetTexture(palette, QPalette.ColorRole.Mid, midImage);
                    SetTexture(palette, QPalette.ColorRole.Window, backgroundImage);

                    QBrush brush = palette.Window;
                    brush.Color = brush.Color.Darker();

                    palette.SetBrush(QPalette.ColorGroup.Disabled, QPalette.ColorRole.WindowText, brush);
                    palette.SetBrush(QPalette.ColorGroup.Disabled, QPalette.ColorRole.Text, brush);
                    palette.SetBrush(QPalette.ColorGroup.Disabled, QPalette.ColorRole.ButtonText, brush);
                    palette.SetBrush(QPalette.ColorGroup.Disabled, QPalette.ColorRole.Base, brush);
                    palette.SetBrush(QPalette.ColorGroup.Disabled, QPalette.ColorRole.Button, brush);
                    palette.SetBrush(QPalette.ColorGroup.Disabled, QPalette.ColorRole.Mid, brush);

                    m_standardPalette = palette;
                }

                return m_standardPalette;
            }
        }

        public override void Polish(QWidget widget)
        {
            if (widget is QPushButton or QComboBox)
                widget.SetAttribute(WidgetAttribute.WA_Hover);
        }

        public override void Unpolish(QWidget widget)
        {
            if (widget is QPushButton or QComboBox)
                widget.SetAttribute(WidgetAttribute.WA_Hover, false);
        }

        public override int pixelMetric(PixelMetric metric, QStyleOption option = null, QWidget widget = null)
        {
            switch (metric) {
                case PixelMetric.PM_ComboBoxFrameWidth:
                    return 8;
                case PixelMetric.PM_ScrollBarExtent:
                    return base.pixelMetric(metric, option, widget) + 4;
                default:
                    return base.pixelMetric(metric, option, widget);
            }
        }

        public override int styleHint(StyleHint hint, QStyleOption option = null, QWidget widget = null, QStyleHintReturn returnData = null)
        {
            switch (hint) {
                case StyleHint.SH_DitherDisabledText:
                    return 0;
                case StyleHint.SH_EtchDisabledText:
                    return 1;
                default:
                    return base.styleHint(hint, option, widget, returnData);
            }
        }
        
        public override void DrawPrimitive(PrimitiveElement element, QStyleOption option, QPainter painter, QWidget widget = null)
        {
            switch (element) 
            {
                case PrimitiveElement.PE_PanelButtonCommand:
                {
                    int delta = (option.State & StateFlag.StateMouseOver) != 0 ? 64 : 0;
                    QColor slightlyOpaqueBlack = new(0, 0, 0, 63);
                    QColor semiTransparentWhite = new(255, 255, 255, 127 + delta);
                    QColor semiTransparentBlack = new(0, 0, 0, 127 - delta);

                    int x = 0, y = 0, width = 0, height = 0;
                    option.Rect.GetRect(ref x, ref y, ref width, ref height);
                    QPainterPath roundRect = RoundRectPath(option.Rect);
                    int radius = Math.Min(width, height) / 2;

                    QBrush brush;
                    bool darker;

                    QStyleOptionButton buttonOption = option as QStyleOptionButton;
                    if (buttonOption != null && (buttonOption.Features & QStyleOptionButton.ButtonFeature.Flat) != 0) {
                        brush = option.Palette.Window;
                        darker = (option.State & (StateFlag.StateSunken | StateFlag.StateOn)) != 0;
                    } else {
                        if ((option.State & (StateFlag.StateSunken | StateFlag.StateOn)) != 0) {
                            brush = option.Palette.Mid;
                            darker = (option.State & StateFlag.StateSunken) == 0;
                        } else {
                            brush = option.Palette.Button;
                            darker = false;
                        }
                    }

                    painter.Save();
                    painter.SetRenderHint(QPainter.RenderHint.Antialiasing);
                    painter.FillPath(roundRect, brush);
                    if (darker)
                        painter.FillPath(roundRect, slightlyOpaqueBlack);

                    int penWidth;
                    if (radius < 10)
                        penWidth = 3;
                    else if (radius < 20)
                        penWidth = 5;
                    else
                        penWidth = 7;

                    QPen topPen = new(semiTransparentWhite, penWidth);
                    QPen bottomPen = new(semiTransparentBlack, penWidth);

                    if ((option.State & (StateFlag.StateSunken | StateFlag.StateOn)) != 0)
                        (topPen, bottomPen) = (bottomPen, topPen);

                    int x1 = x;
                    int x2 = x + radius;
                    int x3 = x + width - radius;
                    int x4 = x + width;

                    if (option.Direction == LayoutDirection.RightToLeft)
                    {
                        (x1, x4) = (x4, x1);
                        (x2, x3) = (x3, x2);
                    }

                    QVector<QPoint> points = new QVector<QPoint>(5);
                    points.Append(new QPoint(x1, y));
                    points.Append(new QPoint(x4, y));
                    points.Append(new QPoint(x3, y + radius));
                    points.Append(new QPoint(x2, y + height - radius));
                    points.Append(new QPoint(x1, y + height));
                    QPolygon topHalf = new(points);

                    painter.SetClipPath(roundRect);
                    painter.SetClipRegion(topHalf, ClipOperation.IntersectClip);
                    painter.Pen = topPen;
                    painter.DrawPath(roundRect);

                    QPolygon bottomHalf = topHalf;
                    bottomHalf[0] = new QPoint(x4, y + height);

                    painter.SetClipPath(roundRect);
                    painter.SetClipRegion(bottomHalf, ClipOperation.IntersectClip);
                    painter.Pen = bottomPen;
                    painter.DrawPath(roundRect);

                    painter.Pen = option.Palette.WindowText.Color;
                    painter.SetClipping(false);
                    painter.DrawPath(roundRect);

                    painter.Restore();
                    break;
                }

                default:
                    base.DrawPrimitive(element, option, painter, widget);
                    break;
            }
        }

        public override void DrawControl(ControlElement element, QStyleOption option, QPainter painter, QWidget widget = null)
        {
            switch (element) 
            {
                case ControlElement.CE_PushButtonLabel:
                {
                    QStyleOptionButton myButtonOption = null;
                    QStyleOptionButton buttonOption = option as QStyleOptionButton;
                    if (buttonOption != null) 
                    {
                        myButtonOption = buttonOption;
                        if (myButtonOption.Palette.CurrentColorGroup != QPalette.ColorGroup.Disabled && 
                            (myButtonOption.State & (StateFlag.StateSunken | StateFlag.StateOn)) != 0) 
                        {
                            myButtonOption.Palette.SetBrush(QPalette.ColorRole.ButtonText, myButtonOption.Palette.BrightText);
                        }
                    }
                    base.DrawControl(element, myButtonOption ?? option, painter, widget);
                    break;
                }
                default:
                    base.DrawControl(element, option, painter, widget);
                    break;
            }
        }

        private static void SetTexture(QPalette palette, QPalette.ColorRole role, QImage image)
        {
            for (int i = 0; i < (int)QPalette.ColorGroup.NColorGroups; ++i) 
            {
                QBrush brush = new(image);
                brush.Color = palette.Brush((QPalette.ColorGroup)i, role).Color;
                palette.SetBrush((QPalette.ColorGroup)i, role, brush);
            }
        }

        private static QPainterPath RoundRectPath(QRect rect)
        {
            int radius = Math.Min(rect.Width, rect.Height) / 2;
            int diam = 2 * radius;

            int x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            rect.GetCoords(ref x1, ref y1, ref x2, ref y2);

            QPainterPath path = new QPainterPath();
            path.MoveTo(x2, y1 + radius);
            path.ArcTo(new QRect(x2 - diam, y1, diam, diam), 0.0, +90.0);
            path.LineTo(x1 + radius, y1);
            path.ArcTo(new QRect(x1, y1, diam, diam), 90.0, +90.0);
            path.LineTo(x1, y2 - radius);
            path.ArcTo(new QRect(x1, y2 - diam, diam, diam), 180.0, +90.0);
            path.LineTo(x1 + radius, y2);
            path.ArcTo(new QRect(x2 - diam, y2 - diam, diam, diam), 270.0, +90.0);
            path.CloseSubpath();
            return path;
        }
    }
}
