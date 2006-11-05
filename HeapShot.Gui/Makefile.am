
CSC = gmcs -debug
CSFLAGS = -target:exe  -r:../HeapShot.Reader/HeapShot.Reader.dll  -r:../HeapShot.Gui.Widgets/HeapShot.Gui.Widgets.dll -pkg:gtk-sharp-2.0

TARGET = HeapShot.Gui.exe
WRAPPER = heap-shot-gui

CSFILES = 			\
	AssemblyInfo.cs		\
	Main.cs		\
	MainWindow.cs		\
	gtk-gui/generated.cs		\
	SelectProcessDialog.cs

bin_SCRIPTS =			\
	$(WRAPPER)

SRCDIR_CSFILES = $(CSFILES:%=$(srcdir)/%)

$(TARGET): $(SRCDIR_CSFILES)
	$(CSC) -out:$@ $(CSFLAGS) $^

$(WRAPPER): $(srcdir)/$(WRAPPER).in
	sed -e "s|\@pkglibdir\@|$(pkglibdir)|g" < $(srcdir)/$(WRAPPER).in > $@
	chmod +x $(WRAPPER)

all: $(TARGET) $(WRAPPER)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_DATA) $(TARGET) $(DESTDIR)$(pkglibdir)

uninstall-local:
	cd $(DESTDIR)$(pkglibdir) && rm -f $(TARGET)


EXTRA_DIST = 			\
	$(CSFILES)		\
	$(WRAPPER).in

CLEANFILES =			\
	$(TARGET)		\
	$(TARGET).mdb		\
	$(WRAPPER)