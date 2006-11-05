
CSC = gmcs -debug
CSFLAGS = -target:library -r:../HeapShot.Reader/HeapShot.Reader.dll  -pkg:gtk-sharp-2.0

TARGET = HeapShot.Gui.Widgets.dll

CSFILES = 			\
	AssemblyInfo.cs		\
	ObjectMapViewer.cs		\
	gtk-gui/generated.cs		\
	ReferenceTreeViewer.cs

RES = gtk-gui/objects.xml \
	gtk-gui/gui.stetic \
	class.png \
	field.png


SRCDIR_CSFILES = $(CSFILES:%=$(srcdir)/%)

$(TARGET): $(SRCDIR_CSFILES)
	$(CSC) -out:$@ $(CSFLAGS) $(RES:%=/resource:%) $^

all: $(TARGET)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_DATA) $(TARGET) $(DESTDIR)$(pkglibdir)

uninstall-local:
	cd $(DESTDIR)$(pkglibdir) && rm -f $(TARGET)

EXTRA_DIST = 			\
	$(CSFILES) $(RES)

CLEANFILES =			\
	$(TARGET)		\
	$(TARGET).mdb
