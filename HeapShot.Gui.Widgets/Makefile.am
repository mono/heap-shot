
CSC = gmcs -debug
CSFLAGS = -target:library 

TARGET = HeapShot.Gui.Widgets.dll

CSFILES =  \
	AssemblyInfo.cs \
	gtk-gui/generated.cs \
	gtk-gui/HeapShot.Gui.Widgets.ObjectMapViewer.cs \
	gtk-gui/HeapShot.Gui.Widgets.ReferenceTreeViewer.cs \
	ObjectMapViewer.cs \
	ReferenceTreeViewer.cs

RES =  \
	class.png \
	field.png \
	gtk-gui/gui.stetic \
	gtk-gui/objects.xml

REFS =  \
	-r:System \
	-pkg:gtk-sharp-2.0 \
	-r:$(top_builddir)/HeapShot.Reader/HeapShot.Reader.dll

SRCDIR_CSFILES = $(CSFILES:%=$(srcdir)/%)
RES_CSFLAGS = $(foreach res, $(RES), -resource:$(res))

$(TARGET): $(SRCDIR_CSFILES) $(RES)
	echo $(RES_FILES)
	$(CSC) -out:$@ $(CSFLAGS) $(REFS) $(RES_CSFLAGS) $(SRCDIR_CSFILES)

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


