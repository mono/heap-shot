
CSC = gmcs -debug
CSFLAGS = -target:exe

TARGET = HeapShot.Gui.exe
WRAPPER = heap-shot-gui

CSFILES =  \
	AssemblyInfo.cs \
	gtk-gui/generated.cs \
	gtk-gui/HeapShot.Gui.SelectProcessDialog.cs \
	gtk-gui/MainWindow.cs \
	Main.cs \
	MainWindow.cs \
	SelectProcessDialog.cs

bin_SCRIPTS =			\
	$(WRAPPER)
	

REFS =  \
	-pkg:gtk-sharp-2.0 \
	-r:$(top_builddir)/HeapShot.Reader/HeapShot.Reader.dll \
	-r:../HeapShot.Gui.Widgets/HeapShot.Gui.Widgets.dll \
	-r:Mono.Posix \
	-r:System

RES = gtk-gui/gui.stetic

SRCDIR_CSFILES = $(CSFILES:%=$(srcdir)/%)

$(TARGET): $(SRCDIR_CSFILES)
	$(CSC) -out:$@ $(CSFLAGS) $(REFS) $^

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
